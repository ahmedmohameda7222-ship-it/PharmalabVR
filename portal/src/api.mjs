// PharmaLabVR English-only university portal. Cloud is identity and academic
// metadata only; it never accepts/synthesizes Native materials or science.
// Membership hints are not authorization. Supabase Auth and PostgreSQL RLS
// remain the server-side authority on every request.
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
const ACCESS = /^[A-Za-z0-9._~-]{1,8192}$/;
const PUBLIC_KEY = /^sb_publishable_[A-Za-z0-9_-]{1,497}$/;
const MAX_BODY_BYTES = 131072;
const ROLES = new Set(['student', 'instructor', 'teaching_assistant', 'university_admin']);

function unavailable() { return new Error('University service unavailable'); }
function invalid(reason) { return new Error('Invalid ' + reason + ' response'); }
function canonicalUuid(value) { return typeof value === 'string' && UUID.test(value) &&
  value !== '00000000-0000-0000-0000-000000000000'; }

async function parseBounded(response) {
  if (!response || !response.ok) throw unavailable();
  const length = Number(response.headers?.get?.('content-length') ?? 0);
  if (!Number.isSafeInteger(length) || length > MAX_BODY_BYTES) throw unavailable();
  const reader = response.body?.getReader?.();
  let text;
  if (reader) {
    const decoder = new TextDecoder('utf-8', { fatal: true });
    let bytes = 0;
    const segments = [];
    try {
      for (;;) {
        const { done, value } = await reader.read();
        if (done) break;
        bytes += value.byteLength;
        if (bytes > MAX_BODY_BYTES) throw unavailable();
        segments.push(decoder.decode(value, { stream: true }));
      }
      segments.push(decoder.decode());
      text = segments.join('');
    } finally {
      reader.releaseLock();
    }
  } else {
    text = await response.text();
    if (new TextEncoder().encode(text).byteLength > MAX_BODY_BYTES) throw unavailable();
  }
  try { return JSON.parse(text); }
  catch { throw invalid('JSON'); }
}

function timestamp(value) {
  if (typeof value !== 'string' ||
      !/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$/.test(value) ||
      !Number.isFinite(Date.parse(value))) throw invalid('academic timestamp');
  return value;
}

export function createUniversityPortalApi({ projectUrl, publishableKey,
  transport = globalThis.fetch, clock = Date.now }) {
  let project;
  try { project = new URL(projectUrl); }
  catch { throw new Error('HTTPS Supabase project URL required'); }
  if (project.protocol !== 'https:' ||
      !/^[a-z0-9-]+\.supabase\.co$/.test(project.hostname) ||
      project.port || project.username || project.password ||
      project.pathname !== '/' || project.search || project.hash)
    throw new Error('HTTPS Supabase project URL required');
  if (typeof publishableKey !== 'string' || !PUBLIC_KEY.test(publishableKey))
    throw new Error('A public sb_publishable_ key is required');
  if (typeof transport !== 'function' || typeof clock !== 'function')
    throw new TypeError('Transport and clock functions are required');

  async function request(path, options = {}, bearer = '') {
    const headers = { apikey: publishableKey, Accept: 'application/json' };
    if (bearer) headers.Authorization = 'Bearer ' + bearer;
    if (options.body !== undefined) headers['Content-Type'] = 'application/json';
    let response;
    try {
      response = await transport(new URL(path, project).toString(), {
        method: options.method ?? 'GET', headers,
        ...(options.body === undefined ? {} : { body: JSON.stringify(options.body) }),
        credentials: 'omit', cache: 'no-store'
      });
      return await parseBounded(response);
    } catch (error) {
      // Never forward server error bodies, passwords or tokens to the UI.
      if (error?.message?.startsWith('Invalid ')) throw error;
      throw unavailable();
    }
  }

  function requireSession(session) {
    if (!session || !canonicalUuid(session.userId) ||
        typeof session.accessToken !== 'string' ||
        !ACCESS.test(session.accessToken) ||
        !Number.isFinite(session.expiresAtMs) ||
        clock() + 30000 >= session.expiresAtMs)
      throw new Error('Student authentication expired; sign in again');
    return session;
  }

  async function signIn(email, password) {
    if (typeof email !== 'string' || !/^[^\s@]{1,128}@[^\s@]{1,253}$/.test(email) ||
        typeof password !== 'string' || password.length < 1 || password.length > 1024)
      throw new Error('Valid email and password required');
    const auth = await request('/auth/v1/token?grant_type=password', {
      method: 'POST', body: { email, password }
    });
    if (auth?.token_type?.toLowerCase() !== 'bearer' ||
        !ACCESS.test(auth.access_token ?? '') ||
        !Number.isSafeInteger(auth.expires_in) ||
        auth.expires_in < 60 || auth.expires_in > 86400 ||
        !canonicalUuid(auth.user?.id)) throw invalid('Auth identity');
    // The token response is not a fresh Auth-server identity check.
    const verified = await request('/auth/v1/user', {}, auth.access_token);
    if (verified?.id !== auth.user.id) throw invalid('Auth user');
    if (!verified.email_confirmed_at && !verified.confirmed_at)
      throw new Error('Verified email required before university access');
    return Object.freeze({
      userId: verified.id,
      accessToken: auth.access_token,
      expiresAtMs: clock() + auth.expires_in * 1000
    });
  }

  async function memberships(session) {
    const current = requireSession(session);
    const url = new URL('/rest/v1/institution_memberships', project);
    url.searchParams.set('select', 'institution_id,role,status');
    url.searchParams.set('user_id', 'eq.' + current.userId);
    url.searchParams.set('status', 'eq.active');
    url.searchParams.set('limit', '128');
    const rows = await request(url.pathname + url.search, {}, current.accessToken);
    if (!Array.isArray(rows) || rows.length > 128) throw invalid('membership');
    const seen = new Set();
    const result = rows.map(row => {
      if (!canonicalUuid(row?.institution_id) ||
          row?.status !== 'active' || !ROLES.has(row?.role) ||
          seen.has(row.institution_id)) throw invalid('membership');
      seen.add(row.institution_id);
      return Object.freeze({ institutionId: row.institution_id, role: row.role });
    });
    return Object.freeze(result);
  }

  async function assignments(session, institutionId) {
    const current = requireSession(session);
    if (!canonicalUuid(institutionId)) throw new Error('Student institution not selected');
    // Refresh the client hint before showing the tenant; the server still
    // independently checks actual membership, enrollment and RLS.
    const active = await memberships(current);
    if (!active.some(row => row.institutionId === institutionId && row.role === 'student'))
      throw new Error('Active student institution required');
    const url = new URL('/rest/v1/assignments', project);
    url.searchParams.set('select', 'id,institution_id,section_id,title,published_at,due_at');
    url.searchParams.set('institution_id', 'eq.' + institutionId);
    url.searchParams.set('published_at', 'not.is.null');
    url.searchParams.set('order', 'due_at.asc');
    url.searchParams.set('limit', '100');
    const rows = await request(url.pathname + url.search, {}, current.accessToken);
    if (!Array.isArray(rows) || rows.length > 100) throw invalid('assignment list');
    const seen = new Set();
    const result = rows.map(row => {
      if (!canonicalUuid(row?.id) || !canonicalUuid(row?.section_id) ||
          row.institution_id !== institutionId ||
          typeof row.title !== 'string' || !row.title.trim() ||
          row.title.length > 200 || seen.has(row.id))
        throw invalid('assignment tenant, identity or title');
      seen.add(row.id);
      return Object.freeze({
        id: row.id, institutionId: institutionId, sectionId: row.section_id,
        title: row.title, publishedAt: timestamp(row.published_at),
        dueAt: row.due_at == null ? null : timestamp(row.due_at)
      });
    });
    return Object.freeze(result);
  }

  // Revoke only the current Auth session's refresh token. An access
  // JWT may still be usable until expiry, even after successful logout.
  async function signOut(session) {
    try {
      const current = requireSession(session);
      const response = await transport(
        new URL('/auth/v1/logout?scope=local', project).toString(),
        {
          method: 'POST',
          headers: {
            apikey: publishableKey,
            Authorization: 'Bearer ' + current.accessToken
          },
          credentials: 'omit', cache: 'no-store'
        });
      return response?.ok === true;
    } catch { return false; }
  }

  return Object.freeze({ signIn, memberships, assignments, signOut });
}
