import test from 'node:test';
import assert from 'node:assert/strict';
import { createUniversityPortalApi } from '../src/api.mjs';

const base = 'https://example.supabase.co/';
const institutionA = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const institutionB = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const student = '11111111-1111-4111-8111-111111111111';
const sectionA = '22222222-2222-4222-8222-222222222222';
const assignmentA = '33333333-3333-4333-8333-333333333333';
const courseA = '44444444-4444-4444-8444-444444444444';
const lessonA = '55555555-5555-4555-8555-555555555555';
const key = 'sb_publishable_test';
const token = 'student.access.token';

function mockResponse(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status, headers: { 'content-type': 'application/json' }
  });
}

function harness(options = {}) {
  const calls = [];
  const transport = async (url, init = {}) => {
    const uri = new URL(url);
    calls.push({ url: uri, init });
    if (uri.pathname === '/auth/v1/token') {
      return mockResponse({
        access_token: token, expires_in: 3600, token_type: 'bearer',
        user: { id: student, email: 'learner@example.edu' }
      });
    }
    if (uri.pathname === '/auth/v1/user') {
      return mockResponse({
        id: student, email: 'learner@example.edu',
        email_confirmed_at: options.unconfirmed ? null : '2026-10-20T01:00:00Z'
      });
    }
    if (uri.pathname === '/auth/v1/logout') {
      if (options.logoutHangs)
        return new Promise((_, reject) => {
          init.signal?.addEventListener('abort', () =>
            reject(new Error('Auth logout canceled')), { once: true });
        });
      if (options.logoutThrows) throw new Error('Simulated offline logout');
      return new Response(null, { status: options.logoutStatus ?? 204 });
    }
    if (uri.pathname === '/rest/v1/institution_memberships') {
      return mockResponse(options.memberships ?? [
        { institution_id: institutionA, role: 'student', status: 'active' },
        { institution_id: institutionB, role: 'instructor', status: 'active' }
      ], options.membershipsStatus ?? 200);
    }
    if (uri.pathname === '/rest/v1/lesson_versions') {
      return mockResponse(options.lessonRows ?? [{
        id: lessonA, institution_id: institutionA, course_id: courseA,
        version: 1, content_sha256: 'a'.repeat(64),
        content_json: { title: 'Lesson instructions', steps: ['Use the Native lab'] },
        published_at: '2026-10-20T02:00:00Z'
      }], options.lessonStatus ?? 200);
    }
    if (uri.pathname === '/rest/v1/assignments') {
      return mockResponse(options.assignments ?? [{
        id: assignmentA, institution_id: institutionA, section_id: sectionA,
        course_id: courseA, lesson_version_id: lessonA,
        title: 'Foundation titration', published_at: '2026-10-20T02:00:00Z',
        due_at: null
      }], options.assignmentsStatus ?? 200);
    }
    throw new Error('Unexpected outbound portal route ' + uri.pathname);
  };
  return { calls, api: createUniversityPortalApi({
    projectUrl: base, publishableKey: key, transport,
    clock: () => Date.parse('2026-10-20T02:30:00Z'),
    requestTimeoutMs: options.requestTimeoutMs ?? 12000
  }) };
}

test('verified GoTrue identity -> fresh membership -> RLS-backed published assignments', async () => {
  const { calls, api } = harness();
  const session = await api.signIn('learner@example.edu', 'private-password');
  assert.equal(session.userId, student);
  assert.equal(session.accessToken, token);
  const memberships = await api.memberships(session);
  assert.equal(memberships.length, 2);
  const rows = await api.assignments(session, institutionA);
  assert.equal(rows.length, 1);
  assert.equal(rows[0].title, 'Foundation titration');
  assert.equal(rows[0].institutionId, institutionA);
  assert.equal(calls[0].url.pathname, '/auth/v1/token');
  assert.equal(calls[0].init.method, 'POST');
  assert.equal(calls[0].init.headers.apikey, key);
  assert.equal(calls[1].url.pathname, '/auth/v1/user');
  assert.equal(calls[1].init.headers.Authorization, 'Bearer ' + token);
  assert.ok(calls.some(x => x.url.pathname === '/rest/v1/institution_memberships' &&
    x.url.searchParams.get('user_id') === 'eq.' + student));
  const assignmentCall = calls.find(x => x.url.pathname === '/rest/v1/assignments');
  assert.equal(assignmentCall.url.searchParams.get('institution_id'), 'eq.' + institutionA);
  assert.equal(assignmentCall.url.searchParams.get('published_at'), 'not.is.null');
  assert.equal(assignmentCall.init.headers.Authorization, 'Bearer ' + token);
});

test('foreign or non-student selection never reaches assignments REST', async () => {
  const { calls, api } = harness();
  const session = await api.signIn('learner@example.edu', 'private-password');
  await assert.rejects(() => api.assignments(session, institutionB), /student/i);
  await assert.rejects(() => api.assignments(session,
    'cccccccc-cccc-4ccc-8ccc-cccccccccccc'), /student/i);
  assert.equal(calls.filter(c => c.url.pathname === '/rest/v1/assignments').length, 0);
});

test('foreign returned assignments fail closed instead of displaying cross-tenant metadata', async () => {
  const { api } = harness({ assignments: [{
    id: assignmentA, institution_id: institutionB, section_id: sectionA,
    title: 'Foreign assignment', published_at: '2026-10-20T02:00:00Z', due_at: null
  }] });
  const session = await api.signIn('learner@example.edu', 'private-password');
  await assert.rejects(() => api.assignments(session, institutionA), /invalid|tenant/i);
});

test('server denial and absent email verification cannot fabricate academic identity', async () => {
  const denied = harness({ assignmentsStatus: 403 });
  const signedIn = await denied.api.signIn('learner@example.edu', 'private-password');
  await assert.rejects(() => denied.api.assignments(signedIn, institutionA), /unavailable/i);
  const unconfirmed = harness({ unconfirmed: true });
  await assert.rejects(() => unconfirmed.api.signIn('learner@example.edu',
    'private-password'), /verified/i);
  assert.equal(unconfirmed.calls.filter(x => x.url.pathname === '/rest/v1/assignments').length, 0);
});

test('expired session cannot issue connected academic requests', async () => {
  const { calls, api } = harness();
  const session = await api.signIn('learner@example.edu', 'private-password');
  const expired = { ...session, expiresAtMs: 1 };
  const before = calls.length;
  await assert.rejects(() => api.memberships(expired), /expired/i);
  assert.equal(calls.length, before);
});

test('client configuration refuses secret keys and non-Supabase hosts', () => {
  assert.throws(() => createUniversityPortalApi({
    projectUrl: base, publishableKey: 'sb_secret_service', transport: async () => {}
  }), /publishable|public/i);
  assert.throws(() => createUniversityPortalApi({
    projectUrl: 'https://evil.example/', publishableKey: key, transport: async () => {}
  }), /Supabase|HTTPS/i);
});

test('browser sign-out requests only current session revocation with authenticated bearer', async () => {
  const { calls, api } = harness();
  const session = await api.signIn('learner@example.edu', 'private-password');
  assert.equal(await api.signOut(session), true);
  const logout = calls.find(c => c.url.pathname === '/auth/v1/logout');
  assert.ok(logout);
  assert.equal(logout.url.searchParams.get('scope'), 'local');
  assert.equal(logout.init.method, 'POST');
  assert.equal(logout.init.headers.Authorization, 'Bearer ' + token);
  assert.equal(logout.init.headers.apikey, key);
});

test('disconnected or expired server logout fails closed without invented confirmation', async () => {
  const { calls, api } = harness({ logoutThrows: true });
  const session = await api.signIn('learner@example.edu', 'private-password');
  assert.equal(await api.signOut(session), false);
  const before = calls.length;
  assert.equal(await api.signOut({ ...session, expiresAtMs: 1 }), false);
  assert.equal(calls.length, before);
});

test('assigned learner can open only pinned published lesson version under tenant RLS', async () => {
  const { api, calls } = harness();
  const session = await api.signIn('learner@example.edu', 'private-password');
  const lesson = await api.assignmentLesson(session, institutionA, assignmentA);
  assert.equal(lesson.assignmentId, assignmentA);
  assert.equal(lesson.id, lessonA);
  assert.equal(lesson.courseId, courseA);
  assert.equal(lesson.version, 1);
  assert.deepEqual(lesson.content, { title: 'Lesson instructions', steps: ['Use the Native lab'] });
  assert.equal(lesson.contentSha256, 'a'.repeat(64));
  const read = calls.find(c => c.url.pathname === '/rest/v1/lesson_versions');
  assert.ok(read);
  assert.equal(read.url.searchParams.get('id'), 'eq.' + lessonA);
  assert.equal(read.url.searchParams.get('institution_id'), 'eq.' + institutionA);
  assert.equal(read.url.searchParams.get('course_id'), 'eq.' + courseA);
  assert.equal(read.url.searchParams.get('published_at'), 'not.is.null');
  assert.equal(read.init.headers.Authorization, 'Bearer ' + token);
  assert.equal(read.init.method, 'GET');
});

test('unselected, foreign or unpublished assignment cannot trigger any lesson read', async () => {
  const { api, calls } = harness();
  const session = await api.signIn('learner@example.edu', 'private-password');
  await assert.rejects(() => api.assignmentLesson(session, institutionB, assignmentA), /student/i);
  await assert.rejects(() => api.assignmentLesson(session, institutionA,
    '99999999-9999-4999-8999-999999999999'), /published assignment/i);
  assert.equal(calls.filter(c => c.url.pathname === '/rest/v1/lesson_versions').length, 0);
});

test('foreign lesson row, wrong pinned course, and missing publication are all refused', async () => {
  const cases = [
    { id: lessonA, institution_id: institutionB, course_id: courseA, version: 1,
      content_sha256: 'a'.repeat(64), content_json: {}, published_at: '2026-10-20T02:00:00Z' },
    { id: lessonA, institution_id: institutionA, course_id: institutionB, version: 1,
      content_sha256: 'a'.repeat(64), content_json: {}, published_at: '2026-10-20T02:00:00Z' },
    { id: lessonA, institution_id: institutionA, course_id: courseA, version: 1,
      content_sha256: 'a'.repeat(64), content_json: {}, published_at: null }
  ];
  for (const row of cases) {
    const { api } = harness({ lessonRows: [row] });
    const session = await api.signIn('learner@example.edu', 'private-password');
    await assert.rejects(() => api.assignmentLesson(session, institutionA, assignmentA),
      /Invalid|unavailable/i);
  }
});

test('RLS denied lesson details fail closed and never fabricate coursework', async () => {
  const { api } = harness({ lessonStatus: 403 });
  const session = await api.signIn('learner@example.edu', 'private-password');
  await assert.rejects(() => api.assignmentLesson(session, institutionA, assignmentA),
    /unavailable/i);
});

// Fail the RED run promptly even when an old implementation never aborts.
const deadline = promise => Promise.race([
  promise,
  new Promise((_, reject) => setTimeout(() =>
    reject(new Error('Request deadline was not enforced')), 400))
]);

test('hung Auth request aborts under bounded request time, never mints signed-in state', async () => {
  let aborted = false;
  const api = createUniversityPortalApi({
    projectUrl: base, publishableKey: key,
    requestTimeoutMs: 25,
    transport: (_url, init) => new Promise((_, reject) => {
      init.signal?.addEventListener('abort', () => {
        aborted = true;
        reject(new Error('Auth connection canceled'));
      }, { once: true });
    })
  });
  await assert.rejects(() => deadline(api.signIn('learner@example.edu', 'private-password')),
    /unavailable/i);
  assert.equal(aborted, true);
});

test('hung current-session logout is bounded and never claims server revocation', async () => {
  const { api } = harness({ logoutHangs: true, requestTimeoutMs: 25 });
  const session = await api.signIn('learner@example.edu', 'private-password');
  assert.equal(await deadline(api.signOut(session)), false);
});
