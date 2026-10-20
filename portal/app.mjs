// Browser controller: all Auth tokens are held in module memory only.
// No localStorage, sessionStorage, cookies, secrets, chemistry or grading.
import { createUniversityPortalApi } from './src/api.mjs';
import { renderAssignments } from './src/view.mjs';
import { projectUrl, publishableKey } from './config.mjs';

const signInForm = document.getElementById('sign-in');
const email = document.getElementById('email');
const password = document.getElementById('password');
const authStatus = document.getElementById('auth-status');
const accountArea = document.getElementById('account');
const institutionSelect = document.getElementById('institution');
const institutionArea = document.getElementById('institution-area');
const assignmentsArea = document.getElementById('assignments');
const clearButton = document.getElementById('clear-session');
let currentSession = null;
let currentGeneration = 0;
let api = null;

function setText(target, value) { target.textContent = value; }
function clearSession() {
  ++currentGeneration;
  currentSession = null;
  institutionSelect.replaceChildren();
  accountArea.hidden = true;
  signInForm.hidden = false;
  password.value = '';
  renderAssignments(assignmentsArea, { status: 'no-student-context' });
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
  } catch {
    if (generation === currentGeneration) {
      currentSession = null;
      setText(authStatus, 'Sign-in or university membership lookup unavailable. Verify your account and retry.');
    }
  } finally {
    signInForm.querySelector('button[type=submit]').disabled = false;
  }
});
institutionSelect.addEventListener('change', async () => {
  const generation = ++currentGeneration;
  const tenant = institutionSelect.value;
  const selected = institutionSelect.selectedOptions[0];
  if (!currentSession || !tenant || selected?.dataset.role !== 'student') {
    renderAssignments(assignmentsArea, { status: 'no-student-context' });
    return;
  }
  renderAssignments(assignmentsArea, { status: 'loading' });
  try {
    const assignments = await api.assignments(currentSession, tenant);
    if (generation === currentGeneration && currentSession &&
        institutionSelect.value === tenant)
      renderAssignments(assignmentsArea, { status: 'available', assignments });
  } catch {
    if (generation === currentGeneration)
      renderAssignments(assignmentsArea, { status: 'unavailable' });
  }
});
