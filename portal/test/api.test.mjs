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
    if (uri.pathname === '/rest/v1/rpc/university_create_course')
      return mockResponse(options.courseResult ??
        '99999999-9999-4999-8999-999999999999', options.courseRpcStatus ?? 200);
    if (uri.pathname === '/rest/v1/rpc/university_enroll_student')
      return mockResponse(options.enrollStatusText ?? 'Enrolled', options.enrollRpcStatus ?? 200);
    if (uri.pathname === '/rest/v1/rpc/university_create_section')
      return mockResponse(options.sectionResult ??
        'aaaaaaaa-aaaa-4aaa-8aaa-000000000001', options.sectionRpcStatus ?? 200);
    if (uri.pathname === '/rest/v1/rpc/university_publish_assignment')
      return mockResponse(options.publishedId ??
        '77777777-7777-4777-8777-777777777777', options.publishStatus ?? 200);
    if (uri.pathname === '/rest/v1/rubric_versions') {
      return mockResponse(options.rubrics ?? [{
        id: '66666666-6666-4666-8666-666666666666',
        institution_id: institutionB, course_id: courseA,
        version: 2, published_at: '2026-10-20T02:00:00Z'
      }], options.rubricStatus ?? 200);
    }
    if (uri.pathname === '/rest/v1/lesson_versions') {
      return mockResponse(options.lessonRows ?? [{
        id: lessonA, institution_id: institutionA, course_id: courseA,
        version: 1, content_sha256: 'a'.repeat(64),
        content_json: { title: 'Lesson instructions', steps: ['Use the Native lab'] },
        published_at: '2026-10-20T02:00:00Z'
      }], options.lessonStatus ?? 200);
    }
    if (uri.pathname === '/rest/v1/submissions')
      return mockResponse(options.submissions ?? [], options.submissionStatus ?? 200);
    if (uri.pathname === '/rest/v1/grades')
      return mockResponse(options.grades ?? [], options.gradeStatus ?? 200);
    if (uri.pathname === '/rest/v1/rpc/university_record_feedback')
      return mockResponse(options.feedbackRecordedId ??
        'aaaaaaaa-aaaa-4aaa-8aaa-000000000002', options.feedbackRpcStatus ?? 200);
    if (uri.pathname === '/rest/v1/feedback')
      return mockResponse(options.feedback ?? [], options.feedbackStatus ?? 200);
    if (uri.pathname === '/rest/v1/courses') {
      return mockResponse(options.courses ?? [{
        id: courseA, institution_id: institutionB,
        code: 'CHEM101', name: 'Foundations', term: 'Fall 2026'
      }], options.coursesStatus ?? 200);
    }
    if (uri.pathname === '/rest/v1/course_sections') {
      return mockResponse(options.sections ?? [{
        id: sectionA, institution_id: institutionB, course_id: courseA,
        section_code: 'LAB01'
      }], options.sectionsStatus ?? 200);
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

test('instructor sees only RLS-visible tenant courses, sections and published assignments', async () => {
  const { api, calls } = harness({ assignments: [{
    id: assignmentA, institution_id: institutionB, section_id: sectionA,
    course_id: courseA, lesson_version_id: lessonA,
    title: 'Controlled lab', published_at: '2026-10-20T02:00:00Z', due_at: null
  }] });
  const session = await api.signIn('instructor@example.edu', 'private-password');
  const overview = await api.staffOverview(session, institutionB);
  assert.equal(overview.institutionId, institutionB);
  assert.equal(overview.role, 'instructor');
  assert.deepEqual(overview.courses.map(x => x.code), ['CHEM101']);
  assert.deepEqual(overview.sections.map(x => x.sectionCode), ['LAB01']);
  assert.deepEqual(overview.assignments.map(x => x.title), ['Controlled lab']);
  for (const route of ['/rest/v1/courses', '/rest/v1/course_sections', '/rest/v1/assignments']) {
    const reads = calls.filter(x => x.url.pathname === route);
    assert.equal(reads.length, 1, route);
    assert.equal(reads[0].url.searchParams.get('institution_id'), 'eq.' + institutionB);
    assert.equal(reads[0].init.headers.Authorization, 'Bearer ' + token);
    assert.equal(reads[0].init.method, 'GET');
  }
  assert.equal(calls.find(c => c.url.pathname === '/rest/v1/assignments')
    .url.searchParams.get('published_at'), 'not.is.null');
});

test('student or other-university selections never reach instructor REST routes', async () => {
  const { api, calls } = harness();
  const session = await api.signIn('learner@example.edu', 'private-password');
  await assert.rejects(() => api.staffOverview(session, institutionA), /staff|instructor/i);
  await assert.rejects(() => api.staffOverview(session,
    'cccccccc-cccc-4ccc-8ccc-cccccccccccc'), /staff|instructor/i);
  assert.equal(calls.filter(x => ['/rest/v1/courses', '/rest/v1/course_sections']
    .includes(x.url.pathname)).length, 0);
});

test('foreign and inconsistent instructor metadata is rejected, never displayed', async () => {
  const cases = [
    { courses: [{ id: courseA, institution_id: institutionA,
      code: 'FOREIGN', name: 'Foreign', term: 'Fall' }] },
    { sections: [{ id: sectionA, institution_id: institutionA,
      course_id: courseA, section_code: 'FOREIGN' }] },
    { sections: [{ id: sectionA, institution_id: institutionB,
      course_id: institutionA, section_code: 'WRONG' }] },
    { courses: [{ id: courseA, institution_id: institutionB,
      code: 'CHEM101', name: 'Foundations', term: 'Fall 2026' },
      { id: courseA, institution_id: institutionB,
      code: 'CHEM102', name: 'Duplicate', term: 'Fall 2026' }] },
    { assignments: [{ id: assignmentA, institution_id: institutionB,
      course_id: courseA, section_id: institutionA,
      title: 'Wrong section', published_at: '2026-10-20T02:00:00Z', due_at: null }] },
    { assignments: [{ id: assignmentA, institution_id: institutionB,
      course_id: courseA, section_id: sectionA,
      title: 'Unpublished', published_at: null, due_at: null }] }
  ];
  for (const options of cases) {
    const { api } = harness(options);
    const session = await api.signIn('instructor@example.edu', 'private-password');
    await assert.rejects(() => api.staffOverview(session, institutionB),
      /invalid|unavailable/i);
  }
});

test('staff metadata RLS denial fails closed without suggesting empty coursework', async () => {
  for (const options of [
    { coursesStatus: 403 }, { sectionsStatus: 403 }, { assignmentsStatus: 403 }
  ]) {
    const { api } = harness(options);
    const session = await api.signIn('instructor@example.edu', 'private-password');
    await assert.rejects(() => api.staffOverview(session, institutionB), /unavailable/i);
  }
});

test('instructor publication options only read already published same-course versions', async () => {
  const { api, calls } = harness({ assignments: [], lessonRows: [{
    id: lessonA, institution_id: institutionB, course_id: courseA,
    version: 1, published_at: '2026-10-20T02:00:00Z'
  }] });
  const session = await api.signIn('teacher@example.edu', 'password');
  const options = await api.staffPublicationOptions(session, institutionB, sectionA);
  assert.equal(options.courseId, courseA);
  assert.equal(options.sectionId, sectionA);
  assert.equal(options.lessons[0].id, lessonA);
  assert.equal(options.rubrics[0].version, 2);
  for (const pathname of ['/rest/v1/lesson_versions', '/rest/v1/rubric_versions']) {
    const query = calls.find(x => x.url.pathname === pathname);
    assert.equal(query.url.searchParams.get('institution_id'), 'eq.' + institutionB);
    assert.equal(query.url.searchParams.get('course_id'), 'eq.' + courseA);
    assert.equal(query.url.searchParams.get('published_at'), 'not.is.null');
  }
});

test('student cannot retrieve instructor publication choices', async () => {
  const { api, calls } = harness();
  const session = await api.signIn('learner@example.edu', 'password');
  await assert.rejects(() => api.staffPublicationOptions(session, institutionA, sectionA),
    /staff|instructor/i);
  assert.equal(calls.filter(x => x.url.pathname === '/rest/v1/rubric_versions').length, 0);
});

test('instructor publication uses existing idempotent RPC, never table INSERT', async () => {
  const rubricId = '66666666-6666-4666-8666-666666666666';
  const requestId = '88888888-8888-4888-8888-888888888888';
  const { api, calls } = harness({ assignments: [], lessonRows: [{
    id: lessonA, institution_id: institutionB, course_id: courseA,
    version: 1, published_at: '2026-10-20T02:00:00Z'
  }] });
  const session = await api.signIn('instructor@example.edu', 'password');
  const result = await api.staffPublishAssignment(session, {
    institutionId: institutionB, sectionId: sectionA,
    lessonVersionId: lessonA, rubricVersionId: rubricId,
    title: '  Laboratory introduction  ', dueAt: null, requestId
  });
  assert.equal(result, '77777777-7777-4777-8777-777777777777');
  const rpc = calls.find(x => x.url.pathname === '/rest/v1/rpc/university_publish_assignment');
  assert.ok(rpc);
  assert.equal(rpc.init.method, 'POST');
  assert.equal(rpc.init.headers.Authorization, 'Bearer ' + token);
  assert.deepEqual(JSON.parse(rpc.init.body), {
    p_institution_id: institutionB, p_section_id: sectionA,
    p_lesson_version_id: lessonA, p_rubric_version_id: rubricId,
    p_title: 'Laboratory introduction', p_due_at: null, p_request_id: requestId
  });
  assert.equal(calls.filter(x => x.url.pathname === '/rest/v1/assignments' &&
    x.init.method !== 'GET').length, 0);
});

test('invalid instructor requests and refused RPC cannot claim publication', async () => {
  const rubricId = '66666666-6666-4666-8666-666666666666';
  const requestId = '88888888-8888-4888-8888-888888888888';
  const options = { assignments: [], lessonRows: [{
    id: lessonA, institution_id: institutionB, course_id: courseA,
    version: 1, published_at: '2026-10-20T02:00:00Z'
  }] };
  const good = { institutionId: institutionB, sectionId: sectionA,
    lessonVersionId: lessonA, rubricVersionId: rubricId,
    title: 'Lesson assignment', dueAt: null, requestId };
  const h = harness(options);
  const session = await h.api.signIn('instructor@example.edu', 'password');
  for (const draft of [
    { ...good, requestId: 'invalid' }, { ...good, title: ' ' },
    { ...good, institutionId: institutionA },
    { ...good, lessonVersionId: institutionA },
    { ...good, dueAt: '2020-01-01T00:00:00Z' }
  ])
    await assert.rejects(() => h.api.staffPublishAssignment(session, draft));
  assert.equal(h.calls.filter(c => c.url.pathname.includes('/rpc/')).length, 0);
  for (const failure of [{ publishStatus: 403 }, { publishedId: 'invalid' }]) {
    const r = harness({ ...options, ...failure });
    const actor = await r.api.signIn('instructor@example.edu', 'password');
    await assert.rejects(() => r.api.staffPublishAssignment(actor, good),
      /unavailable|Invalid/i);
  }
});

test('university admin creates course via existing idempotent audited RPC', async () => {
  const { api, calls } = harness({ memberships: [
    { institution_id: institutionB, role: 'university_admin', status: 'active' }
  ] });
  const session = await api.signIn('admin@example.edu', 'password');
  const requestId = '88888888-8888-4888-8888-888888888888';
  const id = await api.adminCreateCourse(session, {
    institutionId: institutionB, code: 'CHEM101',
    name: '  Lab science  ', term: 'Fall2026', requestId
  });
  assert.equal(id, '99999999-9999-4999-8999-999999999999');
  const rpc = calls.find(x => x.url.pathname === '/rest/v1/rpc/university_create_course');
  assert.equal(rpc.init.method, 'POST');
  assert.equal(rpc.init.headers.Authorization, 'Bearer ' + token);
  assert.deepEqual(JSON.parse(rpc.init.body), {
    p_institution_id: institutionB, p_code: 'CHEM101',
    p_name: 'Lab science', p_term: 'Fall2026', p_request_id: requestId
  });
  assert.equal(calls.some(x => x.url.pathname === '/rest/v1/courses' &&
    x.init.method !== 'GET'), false);
});

test('university admin creates section only for visible same-tenant course via RPC', async () => {
  const h = harness({ memberships: [
    { institution_id: institutionB, role: 'university_admin', status: 'active' }
  ], assignments: [] });
  const session = await h.api.signIn('admin@example.edu', 'password');
  const requestId = '88888888-8888-4888-8888-888888888888';
  const id = await h.api.adminCreateSection(session, {
    institutionId: institutionB, courseId: courseA,
    sectionCode: 'LAB01', requestId
  });
  assert.equal(id, 'aaaaaaaa-aaaa-4aaa-8aaa-000000000001');
  const rpc = h.calls.find(x => x.url.pathname === '/rest/v1/rpc/university_create_section');
  assert.equal(rpc.init.headers.Authorization, 'Bearer ' + token);
  assert.deepEqual(JSON.parse(rpc.init.body), {
    p_institution_id: institutionB, p_course_id: courseA,
    p_section_code: 'LAB01', p_request_id: requestId
  });
});

test('university creation excludes instructor, foreign university and malformed codes', async () => {
  const h = harness();
  const session = await h.api.signIn('instructor@example.edu', 'password');
  await assert.rejects(() => h.api.adminCreateCourse(session, {
    institutionId: institutionB, code: 'CHEM101', name: 'Science',
    term: 'Fall2026', requestId: '88888888-8888-4888-8888-888888888888'
  }), /admin/i);
  await assert.rejects(() => h.api.adminCreateSection(session, {
    institutionId: institutionB, courseId: courseA,
    sectionCode: 'LAB01', requestId: '88888888-8888-4888-8888-888888888888'
  }), /admin/i);
  assert.equal(h.calls.filter(c => c.url.pathname.includes('/rpc/')).length, 0);
  const admin = harness({ memberships: [
    { institution_id: institutionB, role: 'university_admin', status: 'active' }
  ] });
  const adminSession = await admin.api.signIn('admin@example.edu', 'password');
  await assert.rejects(() => admin.api.adminCreateCourse(adminSession, {
    institutionId: institutionB, code: 'lowercase', name: 'Science',
    term: 'Fall2026', requestId: '88888888-8888-4888-8888-888888888888'
  }));
  await assert.rejects(() => admin.api.adminCreateSection(adminSession, {
    institutionId: institutionB, courseId: institutionA,
    sectionCode: 'LAB01', requestId: '88888888-8888-4888-8888-888888888888'
  }));
  assert.equal(admin.calls.filter(c => c.url.pathname.includes('/rpc/')).length, 0);
});

test('university admin enrollment uses existing audited RPC with verified student ID', async () => {
  const h = harness({ memberships: [{
    institution_id: institutionB, role: 'university_admin', status: 'active'
  }], assignments: [] });
  const session = await h.api.signIn('admin@example.edu', 'password');
  const requestId = '88888888-8888-4888-8888-888888888888';
  assert.equal(await h.api.adminEnrollStudent(session, {
    institutionId: institutionB, sectionId: sectionA,
    studentId: student, requestId
  }), 'Enrolled');
  const rpc = h.calls.find(x => x.url.pathname === '/rest/v1/rpc/university_enroll_student');
  assert.equal(rpc.init.method, 'POST');
  assert.equal(rpc.init.headers.Authorization, 'Bearer ' + token);
  assert.deepEqual(JSON.parse(rpc.init.body), {
    p_institution_id: institutionB, p_section_id: sectionA,
    p_student_id: student, p_request_id: requestId
  });
  assert.equal(h.calls.filter(x => x.url.pathname === '/rest/v1/enrollments').length, 0);
});

test('enrollment never trusts instructor, wrong section, refused RPC or invented result', async () => {
  const payload = { institutionId: institutionB, sectionId: sectionA,
    studentId: student, requestId: '88888888-8888-4888-8888-888888888888' };
  const instructor = harness();
  const instructorSession = await instructor.api.signIn('teacher@example.edu', 'password');
  await assert.rejects(() => instructor.api.adminEnrollStudent(instructorSession, payload),
    /admin/i);
  assert.equal(instructor.calls.filter(x => x.url.pathname.includes('/rpc/')).length, 0);
  for (const variants of [
    { enrollRpcStatus: 403 }, { enrollStatusText: 'Unknown' }
  ]) {
    const h = harness({ ...variants, assignments: [], memberships: [{
      institution_id: institutionB, role: 'university_admin', status: 'active'
    }] });
    const session = await h.api.signIn('admin@example.edu', 'password');
    await assert.rejects(() => h.api.adminEnrollStudent(session, payload),
      /unavailable|Invalid/i);
  }
});

test('student views only owned assignment submission and released grade/feedback under RLS', async () => {
  const h = harness({
    submissions: [{ id: '77777777-7777-4777-8777-777777777777', institution_id: institutionA,
      section_id: sectionA, assignment_id: assignmentA, student_id: student,
      submitted_at: '2026-10-21T01:00:00Z' }],
    grades: [{ id: '88888888-8888-4888-8888-888888888888', institution_id: institutionA,
      submission_id: '77777777-7777-4777-8777-777777777777', status: 'approved', score: 87.125 }],
    feedback: [{ id: '99999999-9999-4999-8999-999999999999', institution_id: institutionA,
      submission_id: '77777777-7777-4777-8777-777777777777', body: 'Revise the analysis', student_visible: true,
      created_at: '2026-10-21T02:00:00Z' }]
  });
  const session = await h.api.signIn('learner@example.edu', 'password');
  const result = await h.api.studentResults(session, institutionA, assignmentA);
  assert.equal(result.submissions.length, 1);
  assert.equal(result.grades[0].score, 87.125);
  assert.equal(result.feedback[0].body, 'Revise the analysis');
  for (const resource of ['/rest/v1/submissions', '/rest/v1/grades', '/rest/v1/feedback']) {
    const query = h.calls.find(x => x.url.pathname === resource);
    assert.equal(query.url.searchParams.get('institution_id'), 'eq.' + institutionA);
    assert.equal(query.init.headers.Authorization, 'Bearer ' + token);
    assert.equal(query.init.method, 'GET');
  }
  assert.equal(h.calls.find(x => x.url.pathname === '/rest/v1/submissions')
    .url.searchParams.get('student_id'), 'eq.' + student);
  assert.equal(h.calls.find(x => x.url.pathname === '/rest/v1/grades')
    .url.searchParams.get('status'), 'eq.approved');
  assert.equal(h.calls.find(x => x.url.pathname === '/rest/v1/feedback')
    .url.searchParams.get('student_visible'), 'eq.true');
});

test('student without a recorded submission gets empty academic results, not a fabricated grade', async () => {
  const h = harness();
  const session = await h.api.signIn('learner@example.edu', 'password');
  const result = await h.api.studentResults(session, institutionA, assignmentA);
  assert.deepEqual(result, { submissions: [], grades: [], feedback: [] });
  assert.equal(h.calls.some(c => c.url.pathname === '/rest/v1/grades'), false);
  await assert.rejects(() => h.api.studentResults(session, institutionB, assignmentA), /student/i);
});

test('student rejects foreign student, submission, private feedback, draft grade and bad score rows', async () => {
  const baseSubmission = { id: '77777777-7777-4777-8777-777777777777', institution_id: institutionA,
    section_id: sectionA, assignment_id: assignmentA, student_id: student,
    submitted_at: '2026-10-21T01:00:00Z' };
  const cases = [
    { submissions: [{ ...baseSubmission, student_id: institutionB }] },
    { submissions: [{ ...baseSubmission, institution_id: institutionB }] },
    { submissions: [baseSubmission], grades: [{ id: '88888888-8888-4888-8888-888888888888',
      institution_id: institutionA, submission_id: '77777777-7777-4777-8777-777777777777', status: 'draft', score: 55 }] },
    { submissions: [baseSubmission], grades: [{ id: '88888888-8888-4888-8888-888888888888',
      institution_id: institutionA, submission_id: '77777777-7777-4777-8777-777777777777', status: 'approved', score: 101 }] },
    { submissions: [baseSubmission], feedback: [{ id: '99999999-9999-4999-8999-999999999999',
      institution_id: institutionA, submission_id: '77777777-7777-4777-8777-777777777777', body: 'Secret',
      student_visible: false, created_at: '2026-10-21T02:00:00Z' }] }
  ];
  for (const c of cases) {
    const h = harness(c);
    const session = await h.api.signIn('learner@example.edu', 'password');
    await assert.rejects(() => h.api.studentResults(session, institutionA, assignmentA),
      /Invalid|unavailable/i);
  }
});

test('instructor sees only staff-authorized section submissions; student cannot access', async () => {
  const h = harness({ submissions: [{ id: '77777777-7777-4777-8777-777777777777', institution_id: institutionB,
    course_id: courseA, section_id: sectionA, assignment_id: assignmentA,
    student_id: student, submitted_at: '2026-10-21T01:00:00Z' }],
    assignments: [{ id: assignmentA, institution_id: institutionB,
      course_id: courseA, section_id: sectionA, title: 'Controlled lab',
      published_at: '2026-10-20T02:00:00Z', due_at: null }]
  });
  const session = await h.api.signIn('instructor@example.edu', 'password');
  const rows = await h.api.staffSubmissions(session, institutionB, sectionA);
  assert.equal(rows.length, 1);
  assert.equal(rows[0].studentId, student);
  const req = h.calls.find(c => c.url.pathname === '/rest/v1/submissions');
  assert.equal(req.url.searchParams.get('section_id'), 'eq.' + sectionA);
  assert.equal(req.url.searchParams.get('institution_id'), 'eq.' + institutionB);
  const studentOnly = harness({ memberships: [{ institution_id: institutionB,
    role: 'student', status: 'active' }] });
  const ss = await studentOnly.api.signIn('learner@example.edu', 'password');
  await assert.rejects(() => studentOnly.api.staffSubmissions(ss, institutionB, sectionA),
    /staff/i);
  assert.equal(studentOnly.calls.some(c => c.url.pathname === '/rest/v1/submissions'), false);
});

test('RLS-denied academic result routes remain unavailable, not empty', async () => {
  for (const variants of [{ submissionStatus: 403 }, { gradeStatus: 403 },
    { feedbackStatus: 403 }]) {
    const h = harness({ ...variants, submissions: [{
      id: '77777777-7777-4777-8777-777777777777', institution_id: institutionA, section_id: sectionA,
      assignment_id: assignmentA, student_id: student,
      submitted_at: '2026-10-21T01:00:00Z'
    }] });
    const session = await h.api.signIn('learner@example.edu', 'password');
    await assert.rejects(() => h.api.studentResults(session, institutionA, assignmentA),
      /unavailable/i);
  }
});

test('assigned instructor records immutable visibility-scoped feedback via audited RPC only', async () => {
  const subId = '77777777-7777-4777-8777-777777777777';
  const requestId = '88888888-8888-4888-8888-888888888888';
  const h = harness({
    submissions: [{ id: subId, institution_id: institutionB,
      course_id: courseA, section_id: sectionA, assignment_id: assignmentA,
      student_id: student, submitted_at: '2026-10-21T01:00:00Z' }],
    assignments: [{ id: assignmentA, institution_id: institutionB,
      course_id: courseA, section_id: sectionA, title: 'Controlled lab',
      published_at: '2026-10-20T02:00:00Z', due_at: null }]
  });
  const session = await h.api.signIn('instructor@example.edu', 'password');
  const id = await h.api.staffRecordFeedback(session, {
    institutionId: institutionB, sectionId: sectionA, submissionId: subId,
    body: '  Correct the result discussion. ', studentVisible: false, requestId
  });
  assert.equal(id, 'aaaaaaaa-aaaa-4aaa-8aaa-000000000002');
  const rpc = h.calls.find(x => x.url.pathname === '/rest/v1/rpc/university_record_feedback');
  assert.ok(rpc);
  assert.equal(rpc.init.method, 'POST');
  assert.equal(rpc.init.headers.Authorization, 'Bearer ' + token);
  assert.deepEqual(JSON.parse(rpc.init.body), {
    p_institution_id: institutionB, p_submission_id: subId,
    p_body: '  Correct the result discussion. ',
    p_student_visible: false, p_request_id: requestId
  });
  assert.equal(h.calls.some(x => x.url.pathname === '/rest/v1/feedback' &&
    x.init.method !== 'GET'), false);
});

test('feedback denies TA, foreign institution, missing section and malformed visibility', async () => {
  const subId = '77777777-7777-4777-8777-777777777777';
  const payload = { institutionId: institutionB, sectionId: sectionA,
    submissionId: subId, body: 'Teacher feedback', studentVisible: true,
    requestId: '88888888-8888-4888-8888-888888888888' };
  const ta = harness({ memberships: [{
    institution_id: institutionB, role: 'teaching_assistant', status: 'active'
  }] });
  const taSession = await ta.api.signIn('ta@example.edu', 'password');
  await assert.rejects(() => ta.api.staffRecordFeedback(taSession, payload),
    /instructor|admin/i);
  assert.equal(ta.calls.filter(x => x.url.pathname.includes('/rpc/')).length, 0);
  const h = harness();
  const session = await h.api.signIn('instructor@example.edu', 'password');
  for(const bad of [
    { ...payload, institutionId: institutionA },
    { ...payload, sectionId: institutionA },
    { ...payload, studentVisible: null },
    { ...payload, body: ' ' },
    { ...payload, requestId: 'invalid' }
  ]) await assert.rejects(() => h.api.staffRecordFeedback(session, bad));
  assert.equal(h.calls.filter(x => x.url.pathname.includes('/rpc/')).length, 0);
});

test('feedback RPC 403 or noncanonical result cannot become a confirmed post', async () => {
  const subId = '77777777-7777-4777-8777-777777777777';
  const base = { assignments: [{ id: assignmentA, institution_id: institutionB,
    course_id: courseA, section_id: sectionA, title: 'Controlled lab',
    published_at: '2026-10-20T02:00:00Z', due_at: null }],
    submissions: [{ id: subId, institution_id: institutionB,
      course_id: courseA, section_id: sectionA, assignment_id: assignmentA,
      student_id: student, submitted_at: '2026-10-21T01:00:00Z' }] };
  for(const fault of [{ feedbackRpcStatus: 403 }, { feedbackRecordedId: 'wrong-id' }]) {
    const h = harness({ ...base, ...fault });
    const session = await h.api.signIn('teacher@example.edu', 'password');
    await assert.rejects(() => h.api.staffRecordFeedback(session, {
      institutionId: institutionB, sectionId: sectionA, submissionId: subId,
      body: 'Comments', studentVisible: true,
      requestId: '88888888-8888-4888-8888-888888888888'
    }), /unavailable|Invalid/i);
  }
});

test('approved numeric(8,3) grades remain readable under IEEE-754 rounding', async () => {
  const submissionId = '77777777-7777-4777-8777-777777777777';
  const base = {
    submissions: [{ id: submissionId, institution_id: institutionA,
      section_id: sectionA, assignment_id: assignmentA, student_id: student,
      submitted_at: '2026-10-21T01:00:00Z' }]
  };
  for (const score of [1.005, 99.999, 0.001]) {
    const h = harness({ ...base, grades: [{
      id: '88888888-8888-4888-8888-888888888888',
      institution_id: institutionA, submission_id: submissionId,
      status: 'approved', score
    }] });
    const session = await h.api.signIn('learner@example.edu', 'password');
    assert.equal((await h.api.studentResults(session, institutionA,
      assignmentA)).grades[0].score, score);
  }
  const invalid = harness({ ...base, grades: [{
    id: '88888888-8888-4888-8888-888888888888',
    institution_id: institutionA, submission_id: submissionId,
    status: 'approved', score: 1.0051
  }] });
  const session = await invalid.api.signIn('learner@example.edu', 'password');
  await assert.rejects(() => invalid.api.studentResults(session, institutionA,
    assignmentA), /Invalid/i);
});
