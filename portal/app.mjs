// Browser controller: all Auth tokens are held in module memory only.
// No localStorage, sessionStorage, cookies, secrets, chemistry or grading.
import { createUniversityPortalApi } from './src/api.mjs';
import { renderAssignments, renderLesson, renderStaffOverview } from './src/view.mjs';
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
const clearButton = document.getElementById('clear-session');
let currentSession = null;
let currentGeneration = 0;
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
  staffArea.hidden = true;
  renderStaffOverview(staffOverviewArea, { status: 'no-staff-context' });
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
      ? 'Signed in. Select your active student institution.'
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

institutionSelect.addEventListener('change', async () => {
  const generation = ++currentGeneration;
  const tenant = institutionSelect.value;
  const selected = institutionSelect.selectedOptions[0];
  const role = selected?.dataset.role;
  const staff = ['instructor', 'teaching_assistant', 'university_admin'].includes(role);
  if (!currentSession || !tenant || role !== 'student') {
    renderAssignments(assignmentsArea, { status: 'no-student-context' });
  renderLesson(lessonArea, { status: 'no-selection' });
    staffArea.hidden = !staff;
    renderStaffOverview(staffOverviewArea, { status: staff ? 'loading' : 'no-staff-context' });
    if (staff) {
      const session = currentSession;
      try {
        const overview = await api.staffOverview(session, tenant);
        if (generation === currentGeneration && currentSession === session &&
            institutionSelect.value === tenant)
          renderStaffOverview(staffOverviewArea, { status: 'available', overview });
      } catch {
        if (generation === currentGeneration && currentSession === session)
          renderStaffOverview(staffOverviewArea, { status: 'unavailable' });
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
        document, openPublishedLesson);
  } catch {
    if (generation === currentGeneration)
      renderAssignments(assignmentsArea, { status: 'unavailable' });
  }
});
