// Browser controller: all Auth tokens are held in module memory only.
// No localStorage, sessionStorage, cookies, secrets, chemistry or grading.
import { createUniversityPortalApi } from './src/api.mjs';
import { renderAssignments, renderLesson, renderStaffOverview, renderPublicationForm, renderAdminSetup, renderStudentResults, renderStaffSubmissions, renderFeedbackForm, renderGradeApprovalForm } from './src/view.mjs';
import { projectUrl, publishableKey } from './config.mjs';

const signInForm = document.getElementById('sign-in');
const email = document.getElementById('email');
const password = document.getElementById('password');
const authStatus = document.getElementById('auth-status');
const accountArea = document.getElementById('account');
const institutionSelect = document.getElementById('institution');
const institutionArea = document.getElementById('institution-area');
const assignmentsArea = document.getElementById('assignments');
const lessonArea = document.getElementById('lesson');
const staffArea = document.getElementById('staff-area');
const staffOverviewArea = document.getElementById('staff-overview');
const studentResultsArea = document.getElementById('student-results-area');
const studentResultsBody = document.getElementById('student-results');
const staffSubmissionsArea = document.getElementById('staff-submissions-area');
const staffSubmissionsBody = document.getElementById('staff-submissions');
const feedbackArea = document.getElementById('feedback-area');
const feedbackFormArea = document.getElementById('feedback-form');
const feedbackStatus = document.getElementById('feedback-status');
const gradeArea = document.getElementById('grade-area');
const gradeFormArea = document.getElementById('grade-form');
const gradeStatus = document.getElementById('grade-status');
const publishArea = document.getElementById('publish-area');
const publishChoices = document.getElementById('publish-choices');
const publishStatus = document.getElementById('publish-status');
const adminArea = document.getElementById('admin-area');
const adminSetupArea = document.getElementById('admin-setup');
const adminStatus = document.getElementById('admin-status');
const clearButton = document.getElementById('clear-session');
let currentSession = null;
let currentGeneration = 0;
let publicationSelection = null;
let adminRequests = new Map();
let adminSubmitting = false;
let selectedStaffSection = null;
let feedbackSelection = null;
let gradeSelection = null;
let api = null;

function setText(target, value) { target.textContent = value; }
function clearSession() {
  const toRevoke = currentSession;
  ++currentGeneration;
  currentSession = null;
  // Always clear local memory; HTTP refusal is not a confirmed revoke.
  if (api && toRevoke) void api.signOut(toRevoke);
  institutionSelect.replaceChildren();
  accountArea.hidden = true;
  signInForm.hidden = false;
  password.value = '';
  renderAssignments(assignmentsArea, { status: 'no-student-context' });
  renderLesson(lessonArea, { status: 'no-selection' });
  studentResultsArea.hidden = true;
  staffSubmissionsArea.hidden = true;
  renderStudentResults(studentResultsBody, { status: 'no-selection' });
  renderStaffSubmissions(staffSubmissionsBody, { status: 'no-selection' });
  feedbackArea.hidden = true;
  feedbackSelection = null;
  gradeSelection = null;
  gradeArea.hidden = true;
  renderGradeApprovalForm(gradeFormArea, { status: 'no-selection' });
  setText(gradeStatus, '');
  selectedStaffSection = null;
  renderFeedbackForm(feedbackFormArea, { status: 'no-selection' });
  setText(feedbackStatus, '');
  staffArea.hidden = true;
  renderStaffOverview(staffOverviewArea, { status: 'no-staff-context' });
  adminArea.hidden = true;
  renderAdminSetup(adminSetupArea, { status: 'no-admin-context' });
  publishArea.hidden = true;
  publicationSelection = null;
  renderPublicationForm(publishChoices, { status: 'no-selection' });
  setText(publishStatus, '');
  adminArea.hidden = true;
  adminRequests = new Map();
  renderAdminSetup(adminSetupArea, { status: 'no-admin-context' });
  setText(adminStatus, '');
  setText(authStatus, 'Session cleared from this tab. Sign in to continue.');
}
clearButton.addEventListener('click', clearSession);

try {
  api = createUniversityPortalApi({ projectUrl, publishableKey });
} catch {
  signInForm.hidden = true;
  setText(authStatus, 'University portal not configured. An administrator must supply a public project URL and publishable key.');
}
signInForm.addEventListener('submit', async event => {
  event.preventDefault();
  if (!api) return;
  const generation = ++currentGeneration;
  const submittedEmail = email.value;
  const submittedPassword = password.value;
  password.value = '';
  setText(authStatus, 'Verifying university sign-in…');
  signInForm.querySelector('button[type=submit]').disabled = true;
  try {
    const session = await api.signIn(submittedEmail, submittedPassword);
    if (generation !== currentGeneration) return;
    const memberships = await api.memberships(session);
    if (generation !== currentGeneration) return;
    currentSession = session;
    institutionSelect.replaceChildren();
    const placeholder = document.createElement('option');
    placeholder.value = '';
    placeholder.textContent = 'Select institution';
    institutionSelect.append(placeholder);
    for (const membership of memberships) {
      const option = document.createElement('option');
      option.value = membership.institutionId;
      option.textContent = membership.institutionId + ' — ' + membership.role.replaceAll('_', ' ');
      option.dataset.role = membership.role;
      institutionSelect.append(option);
    }
    institutionArea.hidden = memberships.length === 0;
    signInForm.hidden = true;
    accountArea.hidden = false;
    setText(authStatus, memberships.length > 0
      ? 'Signed in. Select your active institution.'
      : 'Signed in. No active university memberships were found.');
    renderAssignments(assignmentsArea, { status: 'no-student-context' });
  renderLesson(lessonArea, { status: 'no-selection' });
  } catch {
    if (generation === currentGeneration) {
      currentSession = null;
      setText(authStatus, 'Sign-in or university membership lookup unavailable. Verify your account and retry.');
    }
  } finally {
    signInForm.querySelector('button[type=submit]').disabled = false;
  }
});
async function openPublishedLesson(assignmentId) {
  const generation = ++currentGeneration;
  const session = currentSession;
  const tenant = institutionSelect.value;
  const selected = institutionSelect.selectedOptions[0];
  if (!session || !tenant || selected?.dataset.role !== 'student') {
    renderLesson(lessonArea, { status: 'no-selection' });
    return;
  }
  studentResultsArea.hidden = true;
  renderStudentResults(studentResultsBody, { status: 'no-selection' });
  renderLesson(lessonArea, { status: 'loading' });
  try {
    const lesson = await api.assignmentLesson(session, tenant, assignmentId);
    if (generation === currentGeneration && currentSession === session &&
        institutionSelect.value === tenant)
      renderLesson(lessonArea, { status: 'available', lesson });
  } catch {
    if (generation === currentGeneration && currentSession === session)
      renderLesson(lessonArea, { status: 'unavailable' });
  }
}

async function openStudentResults(assignmentId) {
  const session = currentSession;
  const tenant = institutionSelect.value;
  if (!session || !tenant ||
      institutionSelect.selectedOptions[0]?.dataset.role !== 'student') return;
  const generation = ++currentGeneration;
  studentResultsArea.hidden = false;
  renderLesson(lessonArea, { status: 'no-selection' });
  renderStudentResults(studentResultsBody, { status: 'loading' });
  try {
    const results = await api.studentResults(session, tenant, assignmentId);
    if (generation === currentGeneration && currentSession === session &&
        institutionSelect.value === tenant)
      renderStudentResults(studentResultsBody, { status: 'available', results });
  } catch {
    if (generation === currentGeneration && currentSession === session)
      renderStudentResults(studentResultsBody, { status: 'unavailable' });
  }
}

async function submitInstructorFeedback(draft) {
  const selection = feedbackSelection;
  const role = institutionSelect.selectedOptions[0]?.dataset.role;
  const session = currentSession;
  const tenant = institutionSelect.value;
  if (!selection || !session || !tenant ||
      !['instructor', 'university_admin'].includes(role) ||
      draft.submissionId !== selection.submissionId ||
      selection.submitting) return;
  const fingerprint = JSON.stringify(draft);
  if (selection.fingerprint !== fingerprint) {
    selection.fingerprint = fingerprint;
    selection.requestId = crypto.randomUUID();
  }
  selection.submitting = true;
  const generation = currentGeneration;
  setText(feedbackStatus, 'Recording feedback with the university server…');
  try {
    const id = await api.staffRecordFeedback(session, {
      ...draft, institutionId: tenant,
      sectionId: selection.sectionId, requestId: selection.requestId
    });
    if (generation === currentGeneration && currentSession === session &&
        institutionSelect.value === tenant && feedbackSelection === selection)
      setText(feedbackStatus, 'Server confirmed feedback ' + id +
        (draft.studentVisible ? ' — released to student.' : ' — private to authorized staff.'));
  } catch {
    if (generation === currentGeneration && currentSession === session &&
        feedbackSelection === selection)
      setText(feedbackStatus, 'Feedback not confirmed. The same draft retains its request ID on retry.');
  } finally {
    selection.submitting = false;
  }
}

async function submitInstructorGrade(draft) {
  const selection = gradeSelection;
  const session = currentSession;
  const tenant = institutionSelect.value;
  const role = institutionSelect.selectedOptions[0]?.dataset.role;
  if (!selection || !session || !tenant ||
      !['instructor', 'university_admin'].includes(role) ||
      draft.submissionId !== selection.submissionId ||
      selection.sectionId !== selectedStaffSection ||
      selection.submitting || selection.approved) return;

  const fingerprint = JSON.stringify(draft);
  if (selection.fingerprint !== fingerprint) {
    selection.fingerprint = fingerprint;
    selection.requestId = crypto.randomUUID();
  }
  selection.submitting = true;
  const generation = currentGeneration;
  setText(gradeStatus, 'Requesting irreversible university grade approval…');
  try {
    const id = await api.staffApproveGrade(session, {
      ...draft, institutionId: tenant,
      sectionId: selection.sectionId, requestId: selection.requestId
    });
    if (generation === currentGeneration && currentSession === session &&
        institutionSelect.value === tenant && gradeSelection === selection) {
      selection.approved = true;
      setText(gradeStatus, 'University server confirmed immutable academic grade ' +
        id + '. This is not Native scientific attestation.');
    }
  } catch {
    if (generation === currentGeneration && currentSession === session &&
        gradeSelection === selection)
      setText(gradeStatus,
        'Grade NOT confirmed. The unchanged draft keeps its request ID on retry. ' +
        'Reconcile any previous request before creating a new grade.');
  } finally {
    selection.submitting = false;
  }
}

function openGradeForSubmission(submissionId) {
  const role = institutionSelect.selectedOptions[0]?.dataset.role;
  if (!currentSession || !selectedStaffSection ||
      !['instructor', 'university_admin'].includes(role)) return;
  ++currentGeneration;
  feedbackSelection = null;
  feedbackArea.hidden = true;
  renderFeedbackForm(feedbackFormArea, { status: 'no-selection' });
  setText(feedbackStatus, '');
  gradeSelection = { sectionId: selectedStaffSection, submissionId,
    fingerprint: null, requestId: null, submitting: false, approved: false };
  gradeArea.hidden = false;
  setText(gradeStatus, '');
  renderGradeApprovalForm(gradeFormArea, { status: 'available',
    submissionId }, document, submitInstructorGrade);
}

function openFeedbackForSubmission(submissionId) {
  const role = institutionSelect.selectedOptions[0]?.dataset.role;
  if (!currentSession || !selectedStaffSection ||
      !['instructor', 'university_admin'].includes(role)) return;
  ++currentGeneration;
  gradeSelection = null;
  gradeArea.hidden = true;
  renderGradeApprovalForm(gradeFormArea, { status: 'no-selection' });
  setText(gradeStatus, '');
  feedbackSelection = { sectionId: selectedStaffSection, submissionId,
    fingerprint: null, requestId: null, submitting: false };
  feedbackArea.hidden = false;
  setText(feedbackStatus, '');
  renderFeedbackForm(feedbackFormArea, { status: 'available',
    submissionId }, document, submitInstructorFeedback);
}

async function openStaffSubmissions(sectionId) {
  const session = currentSession;
  const tenant = institutionSelect.value;
  const role = institutionSelect.selectedOptions[0]?.dataset.role;
  if (!session || !tenant ||
      !['instructor', 'teaching_assistant', 'university_admin'].includes(role)) return;
  const generation = ++currentGeneration;
  selectedStaffSection = sectionId;
  gradeSelection = null;
  gradeArea.hidden = true;
  renderGradeApprovalForm(gradeFormArea, { status: 'no-selection' });
  setText(gradeStatus, '');
  feedbackSelection = null;
  feedbackArea.hidden = true;
  renderFeedbackForm(feedbackFormArea, { status: 'no-selection' });
  setText(feedbackStatus, '');
  // A different section changes the preview and any instructor publish
  // draft. Never leave a previous section's publication selection active.
  publicationSelection = null;
  publishArea.hidden = true;
  renderPublicationForm(publishChoices, { status: 'no-selection' });
  staffSubmissionsArea.hidden = false;
  renderStaffSubmissions(staffSubmissionsBody, { status: 'loading' });
  try {
    const submissions = await api.staffSubmissions(session, tenant, sectionId);
    if (generation === currentGeneration && currentSession === session &&
        institutionSelect.value === tenant)
      renderStaffSubmissions(staffSubmissionsBody, {
        status: 'available', submissions, role }, document,
        openFeedbackForSubmission, openGradeForSubmission);
  } catch {
    if (generation === currentGeneration && currentSession === session)
      renderStaffSubmissions(staffSubmissionsBody, { status: 'unavailable' });
  }
}

async function createAcademicMetadata(kind, draft) {
  const session = currentSession;
  const tenant = institutionSelect.value;
  const role = institutionSelect.selectedOptions[0]?.dataset.role;
  if (!session || !tenant || role !== 'university_admin' || adminSubmitting) return;
  const generation = currentGeneration;
  const fingerprint = kind + ':' + JSON.stringify(draft);
  if (!adminRequests.has(fingerprint)) {
    if (adminRequests.size >= 32) adminRequests = new Map();
    adminRequests.set(fingerprint, crypto.randomUUID());
  }
  const requestId = adminRequests.get(fingerprint);
  adminSubmitting = true;
  setText(adminStatus, 'Submitting to the university academic server…');
  try {
    const params = { ...draft, institutionId: tenant, requestId };
    const createdId = kind === 'course'
      ? await api.adminCreateCourse(session, params)
      : kind === 'section'
        ? await api.adminCreateSection(session, params)
        : await api.adminEnrollStudent(session, params);
    if (generation === currentGeneration && session === currentSession &&
        institutionSelect.value === tenant) {
      setText(adminStatus, 'Created ' + kind + ' confirmed by server: ' + createdId);
      const overview = await api.staffOverview(session, tenant);
      if (generation === currentGeneration && session === currentSession) {
        renderStaffOverview(staffOverviewArea, { status: 'available', overview },
          document, openStaffSection, openStaffSubmissions);
        renderAdminSetup(adminSetupArea, { status: 'available',
          courses: overview.courses, sections: overview.sections }, document,
          draft => createAcademicMetadata('course', draft),
          draft => createAcademicMetadata('section', draft),
          draft => createAcademicMetadata('enrollment', draft));
      }
    }
  } catch {
    if (generation === currentGeneration && session === currentSession)
      setText(adminStatus,
        'Creation not confirmed. An unchanged retry retains the same request ID.');
  } finally {
    adminSubmitting = false;
  }
}

function publicationContext() {
  const role = institutionSelect.selectedOptions[0]?.dataset.role;
  return currentSession && ['university_admin', 'instructor'].includes(role)
    ? { session: currentSession, tenant: institutionSelect.value } : null;
}

async function submitPublication(draft) {
  const ctx = publicationContext();
  const selection = publicationSelection;
  if (!ctx || !selection || draft.sectionId !== selection.sectionId ||
      selection.submitting) return;
  const fingerprint = JSON.stringify(draft);
  if (selection.fingerprint !== fingerprint) {
    // Reuse this idempotency key after timeouts for the exact same draft.
    selection.fingerprint = fingerprint;
    selection.requestId = crypto.randomUUID();
  }
  selection.submitting = true;
  const generation = currentGeneration;
  setText(publishStatus, 'Submitting assignment to the university server…');
  try {
    const id = await api.staffPublishAssignment(ctx.session, {
      ...draft, institutionId: ctx.tenant, requestId: selection.requestId
    });
    if (generation === currentGeneration && publicationSelection === selection &&
        currentSession === ctx.session && institutionSelect.value === ctx.tenant) {
      setText(publishStatus, 'Published assignment confirmed by server: ' + id);
      const overview = await api.staffOverview(ctx.session, ctx.tenant);
      if (generation === currentGeneration && publicationSelection === selection)
        renderStaffOverview(staffOverviewArea, { status: 'available', overview },
          document, openStaffSection, openStaffSubmissions);
    }
  } catch {
    if (generation === currentGeneration && publicationSelection === selection)
      setText(publishStatus,
        'Publication not confirmed. Review the request and retry; unchanged requests reuse the same ID.');
  } finally {
    selection.submitting = false;
  }
}

async function openStaffSection(sectionId) {
  const ctx = publicationContext();
  if (!ctx) return;
  const generation = ++currentGeneration;
  const selection = { sectionId, fingerprint: null, requestId: null, submitting: false };
  publicationSelection = selection;
  selectedStaffSection = null;
  feedbackSelection = null;
  feedbackArea.hidden = true;
  renderFeedbackForm(feedbackFormArea, { status: 'no-selection' });
  staffSubmissionsArea.hidden = true;
  renderStaffSubmissions(staffSubmissionsBody, { status: 'no-selection' });
  publishArea.hidden = false;
  setText(publishStatus, '');
  renderPublicationForm(publishChoices, { status: 'loading' });
  try {
    const options = await api.staffPublicationOptions(ctx.session, ctx.tenant, sectionId);
    if (generation === currentGeneration && publicationSelection === selection &&
        currentSession === ctx.session && institutionSelect.value === ctx.tenant)
      renderPublicationForm(publishChoices, { status: 'available', options },
        document, submitPublication);
  } catch {
    if (generation === currentGeneration && publicationSelection === selection)
      renderPublicationForm(publishChoices, { status: 'unavailable' });
  }
}

institutionSelect.addEventListener('change', async () => {
  const generation = ++currentGeneration;
  gradeSelection = null;
  gradeArea.hidden = true;
  renderGradeApprovalForm(gradeFormArea, { status: 'no-selection' });
  setText(gradeStatus, '');
  feedbackArea.hidden = true;
  feedbackSelection = null;
  selectedStaffSection = null;
  renderFeedbackForm(feedbackFormArea, { status: 'no-selection' });
  setText(feedbackStatus, '');
  studentResultsArea.hidden = true;
  staffSubmissionsArea.hidden = true;
  renderStudentResults(studentResultsBody, { status: 'no-selection' });
  renderStaffSubmissions(staffSubmissionsBody, { status: 'no-selection' });
  publicationSelection = null;
  adminRequests = new Map();
  adminArea.hidden = true;
  setText(adminStatus, '');
  renderAdminSetup(adminSetupArea, { status: 'no-admin-context' });
  publishArea.hidden = true;
  setText(publishStatus, '');
  renderPublicationForm(publishChoices, { status: 'no-selection' });
  const tenant = institutionSelect.value;
  const selected = institutionSelect.selectedOptions[0];
  const role = selected?.dataset.role;
  const staff = ['instructor', 'teaching_assistant', 'university_admin'].includes(role);
  if (!currentSession || !tenant || role !== 'student') {
    renderAssignments(assignmentsArea, { status: 'no-student-context' });
  renderLesson(lessonArea, { status: 'no-selection' });
    staffArea.hidden = !staff;
    adminArea.hidden = role !== 'university_admin';
    if (role === 'university_admin')
      renderAdminSetup(adminSetupArea, { status: 'loading' });
    renderStaffOverview(staffOverviewArea, { status: staff ? 'loading' : 'no-staff-context' });
    if (staff) {
      const session = currentSession;
      try {
        const overview = await api.staffOverview(session, tenant);
        if (generation === currentGeneration && currentSession === session &&
            institutionSelect.value === tenant)
          renderStaffOverview(staffOverviewArea, { status: 'available', overview },
            document, openStaffSection, openStaffSubmissions);
        if (generation === currentGeneration && role === 'university_admin')
          renderAdminSetup(adminSetupArea, { status: 'available',
            courses: overview.courses, sections: overview.sections }, document,
            draft => createAcademicMetadata('course', draft),
            draft => createAcademicMetadata('section', draft),
            draft => createAcademicMetadata('enrollment', draft));
      } catch {
        if (generation === currentGeneration && currentSession === session)
          renderStaffOverview(staffOverviewArea, { status: 'unavailable' });
        if (generation === currentGeneration && role === 'university_admin')
          renderAdminSetup(adminSetupArea, { status: 'unavailable' });
      }
    }
    return;
  }
  staffArea.hidden = true;
  renderStaffOverview(staffOverviewArea, { status: 'no-staff-context' });
  renderLesson(lessonArea, { status: 'no-selection' });
  renderAssignments(assignmentsArea, { status: 'loading' });
  const session = currentSession;
  try {
    const assignments = await api.assignments(session, tenant);
    if (generation === currentGeneration && currentSession === session &&
        institutionSelect.value === tenant)
      renderAssignments(assignmentsArea, { status: 'available', assignments },
        document, openPublishedLesson, openStudentResults);
  } catch {
    if (generation === currentGeneration)
      renderAssignments(assignmentsArea, { status: 'unavailable' });
  }
});
