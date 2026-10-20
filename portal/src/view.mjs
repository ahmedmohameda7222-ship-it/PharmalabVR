// English-only academic metadata rendering. Never use HTML interpolation.
export function renderAssignments(target, state, documentSource = globalThis.document) {
  if (!target || typeof target.replaceChildren !== 'function' ||
      !documentSource || typeof documentSource.createElement !== 'function')
    throw new TypeError('A DOM target and document are required');
  const status = state?.status;
  const assignments = state?.assignments ?? [];
  if (!['loading', 'available', 'unavailable', 'no-student-context'].includes(status))
    throw new TypeError('Recognized assignment status required');
  const message = text => {
    const paragraph = documentSource.createElement('p');
    paragraph.textContent = text;
    target.replaceChildren(paragraph);
  };
  if (status === 'loading') { message('Loading published assignments…'); return; }
  if (status === 'unavailable') {
    message('Assignments could not be loaded. Check the connection and try again.');
    return;
  }
  if (status === 'no-student-context') {
    message('Select an active student institution to see published assignments.');
    return;
  }
  if (!Array.isArray(assignments)) throw new TypeError('Assignment rows required');
  if (assignments.length === 0) { message('No published assignments for this institution.'); return; }
  const list = documentSource.createElement('ul');
  list.className = 'assignment-list';
  for (const assignment of assignments) {
    const item = documentSource.createElement('li');
    const heading = documentSource.createElement('h3');
    heading.textContent = assignment.title;
    const dates = documentSource.createElement('p');
    dates.textContent = assignment.dueAt
      ? 'Due: ' + new Date(assignment.dueAt).toLocaleString('en', { dateStyle: 'medium', timeStyle: 'short' })
      : 'Due date not set';
    item.append(heading, dates);
    list.append(item);
  }
  target.replaceChildren(list);
}
