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
  transport = globalThis.fetch, clock = Date.now, requestTimeoutMs = 12000 }) {
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
  if (!Number.isInteger(requestTimeoutMs) ||
      requestTimeoutMs < 10 || requestTimeoutMs > 60000)
    throw new RangeError('Bounded request deadline required');

  // Deadline covers both response headers and bounded streaming body.
  // Real browser fetch observes AbortSignal; synthetic CI handlers exercise
  // the same cancellation pathway. No offline response becomes an Auth fact.
  async function withDeadline(operation) {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), requestTimeoutMs);
    try { return await operation(controller.signal); }
    finally { clearTimeout(timer); }
  }

  async function request(path, options = {}, bearer = '') {
    const headers = { apikey: publishableKey, Accept: 'application/json' };
    if (bearer) headers.Authorization = 'Bearer ' + bearer;
    if (options.body !== undefined) headers['Content-Type'] = 'application/json';
    let response;
    try {
      return await withDeadline(async signal => {
        response = await transport(new URL(path, project).toString(), {
          method: options.method ?? 'GET', headers,
          ...(options.body === undefined ? {} : { body: JSON.stringify(options.body) }),
          credentials: 'omit', cache: 'no-store', signal
        });
        return await parseBounded(response);
      });
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

  // Role is a UI hint only: PostgREST SELECTs use authenticated JWT and
  // existing institution-scoped policies. No service key or direct writes.
  // This is a bounded overview of the first 100 RLS-visible rows per type,
  // NOT an assertion that the user's university has only these records.
  async function staffOverview(session, institutionId) {
    const current = requireSession(session);
    if (!canonicalUuid(institutionId))
      throw new Error('Active instructor institution required');
    const active = await memberships(current);
    const staff = active.find(row => row.institutionId === institutionId &&
      ['university_admin', 'instructor', 'teaching_assistant'].includes(row.role));
    if (!staff) throw new Error('Active university staff institution required');

    async function read(resource, select, filters = {}) {
      const url = new URL('/rest/v1/' + resource, project);
      url.searchParams.set('select', select);
      url.searchParams.set('institution_id', 'eq.' + institutionId);
      for (const [key, value] of Object.entries(filters))
        url.searchParams.set(key, value);
      url.searchParams.set('limit', '100');
      const rows = await request(url.pathname + url.search, {}, current.accessToken);
      if (!Array.isArray(rows) || rows.length > 100)
        throw invalid(resource + ' staff list');
      return rows;
    }

    const courseRows = await read('courses', 'id,institution_id,code,name,term');
    const seenCourses = new Set();
    const courses = courseRows.map(row => {
      if (!canonicalUuid(row?.id) || row.institution_id !== institutionId ||
          seenCourses.has(row.id) ||
          typeof row.code !== 'string' || !row.code.trim() || row.code.length > 40 ||
          typeof row.name !== 'string' || !row.name.trim() || row.name.length > 200 ||
          typeof row.term !== 'string' || !row.term.trim() || row.term.length > 160)
        throw invalid('staff course');
      seenCourses.add(row.id);
      return Object.freeze({ id: row.id, institutionId,
        code: row.code, name: row.name, term: row.term });
    });

    const sectionRows = await read('course_sections',
      'id,institution_id,course_id,section_code');
    const seenSections = new Map();
    const sections = sectionRows.map(row => {
      if (!canonicalUuid(row?.id) || row.institution_id !== institutionId ||
          !seenCourses.has(row.course_id) || seenSections.has(row.id) ||
          typeof row.section_code !== 'string' ||
          !row.section_code.trim() || row.section_code.length > 160)
        throw invalid('staff section');
      seenSections.set(row.id, row.course_id);
      return Object.freeze({ id: row.id, institutionId,
        courseId: row.course_id, sectionCode: row.section_code });
    });

    const assignmentRows = await read('assignments',
      'id,institution_id,course_id,section_id,title,published_at,due_at',
      { published_at: 'not.is.null' });
    const seenAssignments = new Set();
    const assignments = assignmentRows.map(row => {
      if (!canonicalUuid(row?.id) || row.institution_id !== institutionId ||
          seenAssignments.has(row.id) || !canonicalUuid(row.course_id) ||
          !canonicalUuid(row.section_id) ||
          seenSections.get(row.section_id) !== row.course_id ||
          typeof row.title !== 'string' || !row.title.trim() ||
          row.title.length > 200)
        throw invalid('staff assignment');
      seenAssignments.add(row.id);
      return Object.freeze({ id: row.id, institutionId,
        courseId: row.course_id, sectionId: row.section_id,
        title: row.title, publishedAt: timestamp(row.published_at),
        dueAt: row.due_at == null ? null : timestamp(row.due_at) });
    });
    return Object.freeze({ institutionId, role: staff.role,
      courses: Object.freeze(courses), sections: Object.freeze(sections),
      assignments: Object.freeze(assignments) });
  }

  // Existing server-published immutable versions, for an instructor
  // selecting a course-bound assignment. RLS is enforced per SELECT.
  // This does not publish, grade or amend scientific evidence.
  async function staffPublicationOptions(session, institutionId, sectionId) {
    const current = requireSession(session);
    if (!canonicalUuid(sectionId)) throw new Error('Instructor section required');
    const overview = await staffOverview(current, institutionId);
    if (overview.role !== 'instructor' && overview.role !== 'university_admin')
      throw new Error('Instructor or administrator required');
    const section = overview.sections.find(row => row.id === sectionId);
    if (!section) throw new Error('Instructor section unavailable');

    async function read(resource) {
      const url = new URL('/rest/v1/' + resource, project);
      url.searchParams.set('select',
        'id,institution_id,course_id,version,published_at');
      url.searchParams.set('institution_id', 'eq.' + institutionId);
      url.searchParams.set('course_id', 'eq.' + section.courseId);
      url.searchParams.set('published_at', 'not.is.null');
      url.searchParams.set('order', 'version.desc');
      url.searchParams.set('limit', '100');
      const rows = await request(url.pathname + url.search, {},
        current.accessToken);
      if (!Array.isArray(rows) || rows.length > 100)
        throw invalid(resource + ' published versions');
      const seen = new Set();
      return Object.freeze(rows.map(row => {
        if (!canonicalUuid(row?.id) || seen.has(row.id) ||
            row.institution_id !== institutionId ||
            row.course_id !== section.courseId ||
            !Number.isSafeInteger(row.version) ||
            row.version < 1 || row.version > 1000000)
          throw invalid(resource + ' identity');
        seen.add(row.id);
        return Object.freeze({ id: row.id, courseId: section.courseId,
          version: row.version, publishedAt: timestamp(row.published_at) });
      }));
    }
    const lessons = await read('lesson_versions');
    const rubrics = await read('rubric_versions');
    return Object.freeze({ institutionId, sectionId,
      courseId: section.courseId, lessons, rubrics });
  }

  // The existing server-side RPC enforces instructor/section authority,
  // immutable published version pins, audit and exact-request idempotency.
  // No direct academic table write and no claimed Native attestation.
  async function staffPublishAssignment(session, draft) {
    const current = requireSession(session);
    if (!draft || !canonicalUuid(draft.institutionId) ||
        !canonicalUuid(draft.sectionId) ||
        !canonicalUuid(draft.lessonVersionId) ||
        !canonicalUuid(draft.rubricVersionId) ||
        !canonicalUuid(draft.requestId) ||
        typeof draft.title !== 'string' ||
        draft.title.trim().length < 2 || draft.title.trim().length > 200)
      throw new Error('Invalid instructor assignment publication request');
    const title = draft.title.trim();
    let dueAt = null;
    if (draft.dueAt != null) {
      dueAt = timestamp(draft.dueAt);
      if (Date.parse(dueAt) <= clock())
        throw new Error('Assignment due date must be in the future');
    }
    // Client checks are fail-closed presentation constraints. Real
    // authorization and request replay detection run on the RPC server.
    const choices = await staffPublicationOptions(current,
      draft.institutionId, draft.sectionId);
    if (!choices.lessons.some(x => x.id === draft.lessonVersionId) ||
        !choices.rubrics.some(x => x.id === draft.rubricVersionId))
      throw new Error('Published same-course lesson and rubric required');

    const result = await request('/rest/v1/rpc/university_publish_assignment', {
      method: 'POST',
      body: {
        p_institution_id: draft.institutionId,
        p_section_id: draft.sectionId,
        p_lesson_version_id: draft.lessonVersionId,
        p_rubric_version_id: draft.rubricVersionId,
        p_title: title,
        p_due_at: dueAt,
        p_request_id: draft.requestId
      }
    }, current.accessToken);
    if (!canonicalUuid(result)) throw invalid('assignment publication identity');
    return result;
  }

  // Academic creation is authorized and audited in the already-deployed
  // security-definer RPCs, never via client INSERT or service-role keys.
  async function requireUniversityAdmin(session, institutionId) {
    const current = requireSession(session);
    if (!canonicalUuid(institutionId))
      throw new Error('Active university administrator required');
    const active = await memberships(current);
    if (!active.some(m => m.institutionId === institutionId &&
        m.role === 'university_admin'))
      throw new Error('Active university administrator required');
    return current;
  }

  async function adminCreateCourse(session, draft) {
    if (!draft || !canonicalUuid(draft.institutionId) ||
        !canonicalUuid(draft.requestId) ||
        typeof draft.code !== 'string' ||
        !/^[A-Z0-9][A-Z0-9_-]{0,39}$/.test(draft.code) ||
        typeof draft.term !== 'string' ||
        !/^[A-Za-z0-9_-]{1,32}$/.test(draft.term) ||
        typeof draft.name !== 'string' ||
        draft.name.trim().length < 2 || draft.name.trim().length > 200)
      throw new Error('Invalid university course request');
    const current = await requireUniversityAdmin(session, draft.institutionId);
    const result = await request('/rest/v1/rpc/university_create_course', {
      method: 'POST', body: {
        p_institution_id: draft.institutionId,
        p_code: draft.code, p_name: draft.name.trim(),
        p_term: draft.term, p_request_id: draft.requestId
      }
    }, current.accessToken);
    if (!canonicalUuid(result)) throw invalid('created course identity');
    return result;
  }

  async function adminCreateSection(session, draft) {
    if (!draft || !canonicalUuid(draft.institutionId) ||
        !canonicalUuid(draft.courseId) ||
        !canonicalUuid(draft.requestId) ||
        typeof draft.sectionCode !== 'string' ||
        !/^[A-Z0-9][A-Z0-9_-]{0,19}$/.test(draft.sectionCode))
      throw new Error('Invalid university section request');
    const current = await requireUniversityAdmin(session, draft.institutionId);
    const overview = await staffOverview(current, draft.institutionId);
    if (overview.role !== 'university_admin' ||
        !overview.courses.some(row => row.id === draft.courseId))
      throw new Error('Administrator course is not accessible');
    const result = await request('/rest/v1/rpc/university_create_section', {
      method: 'POST', body: {
        p_institution_id: draft.institutionId,
        p_course_id: draft.courseId,
        p_section_code: draft.sectionCode,
        p_request_id: draft.requestId
      }
    }, current.accessToken);
    if (!canonicalUuid(result)) throw invalid('created section identity');
    return result;
  }

  // Existing GoTrue-verified student must already hold an active student
  // membership. The university admin RPC validates that server-side,
  // including the section, verified email and exact request replay.
  async function adminEnrollStudent(session, draft) {
    if (!draft || !canonicalUuid(draft.institutionId) ||
        !canonicalUuid(draft.sectionId) ||
        !canonicalUuid(draft.studentId) ||
        !canonicalUuid(draft.requestId))
      throw new Error('Invalid university student enrollment request');
    const current = await requireUniversityAdmin(session, draft.institutionId);
    const overview = await staffOverview(current, draft.institutionId);
    if (overview.role !== 'university_admin' ||
        !overview.sections.some(x => x.id === draft.sectionId))
      throw new Error('Administrator section is unavailable');
    const result = await request('/rest/v1/rpc/university_enroll_student', {
      method: 'POST', body: {
        p_institution_id: draft.institutionId,
        p_section_id: draft.sectionId,
        p_student_id: draft.studentId,
        p_request_id: draft.requestId
      }
    }, current.accessToken);
    if (result !== 'Enrolled' && result !== 'AlreadyEnrolled')
      throw invalid('enrollment confirmation');
    return result;
  }

  // Read-only detail of an assigned and published lesson. First confirm
  // assignment visibility through the student's fresh tenant-scoped RLS list,
  // then read the persisted assignment's pinned lesson_version_id and
  // course_id before requesting the immutable published lesson. These
  // client checks never replace Auth, section-enrollment or lesson RLS.
  async function assignmentLesson(session, institutionId, assignmentId) {
    const current = requireSession(session);
    if (!canonicalUuid(institutionId) || !canonicalUuid(assignmentId))
      throw new Error('Published assignment identity is invalid');

    const list = await assignments(current, institutionId);
    const assigned = list.find(row => row.id === assignmentId);
    if (!assigned) throw new Error('Published assignment is not accessible');

    const detailUrl = new URL('/rest/v1/assignments', project);
    detailUrl.searchParams.set('select',
      'id,institution_id,section_id,course_id,lesson_version_id,published_at');
    detailUrl.searchParams.set('id', 'eq.' + assignmentId);
    detailUrl.searchParams.set('institution_id', 'eq.' + institutionId);
    detailUrl.searchParams.set('published_at', 'not.is.null');
    detailUrl.searchParams.set('limit', '2');
    const details = await request(detailUrl.pathname + detailUrl.search, {},
      current.accessToken);
    if (!Array.isArray(details) || details.length !== 1 ||
        details[0]?.id !== assignmentId ||
        details[0]?.institution_id !== institutionId ||
        details[0]?.section_id !== assigned.sectionId ||
        !canonicalUuid(details[0]?.course_id) ||
        !canonicalUuid(details[0]?.lesson_version_id))
      throw invalid('published assignment detail');
    timestamp(details[0].published_at);

    const lessonId = details[0].lesson_version_id;
    const courseId = details[0].course_id;
    const lessonUrl = new URL('/rest/v1/lesson_versions', project);
    lessonUrl.searchParams.set('select',
      'id,institution_id,course_id,version,content_sha256,content_json,published_at');
    lessonUrl.searchParams.set('id', 'eq.' + lessonId);
    lessonUrl.searchParams.set('institution_id', 'eq.' + institutionId);
    lessonUrl.searchParams.set('course_id', 'eq.' + courseId);
    lessonUrl.searchParams.set('published_at', 'not.is.null');
    lessonUrl.searchParams.set('limit', '2');
    const rows = await request(lessonUrl.pathname + lessonUrl.search, {},
      current.accessToken);
    if (!Array.isArray(rows) || rows.length !== 1) throw invalid('lesson visibility');
    const row = rows[0];
    if (row?.id !== lessonId ||
        row?.institution_id !== institutionId ||
        row?.course_id !== courseId ||
        !Number.isSafeInteger(row.version) || row.version < 1 || row.version > 10000 ||
        typeof row.content_sha256 !== 'string' ||
        !/^[0-9a-f]{64}$/.test(row.content_sha256) ||
        row.content_json === null || Array.isArray(row.content_json) ||
        typeof row.content_json !== 'object')
      throw invalid('lesson provenance and content');
    timestamp(row.published_at);

    // The server's content_sha256 is the PostgreSQL jsonb::text digest.
    // A JS JSON.stringify digest is NOT the same canonicalization and is
    // deliberately not claimed as locally verified scientific provenance.
    return Object.freeze({
      assignmentId, id: lessonId, institutionId, courseId,
      version: row.version, contentSha256: row.content_sha256,
      publishedAt: row.published_at, content: row.content_json
    });
  }

  // Revoke only the current Auth session's refresh token. An access
  // JWT may still be usable until expiry, even after successful logout.
  async function signOut(session) {
    try {
      const current = requireSession(session);
      return await withDeadline(async signal => {
        const response = await transport(
          new URL('/auth/v1/logout?scope=local', project).toString(),
          {
            method: 'POST',
            headers: {
              apikey: publishableKey,
              Authorization: 'Bearer ' + current.accessToken
            },
            credentials: 'omit', cache: 'no-store', signal
          });
        return response?.ok === true;
      });
    } catch { return false; }
  }

  return Object.freeze({ signIn, memberships, assignments, staffOverview, staffPublicationOptions, staffPublishAssignment, adminCreateCourse, adminCreateSection, adminEnrollStudent, assignmentLesson, signOut });
}
