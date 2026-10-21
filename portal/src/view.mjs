// English-only academic metadata rendering. Never use HTML interpolation.
export function renderAssignments(target, state, documentSource = globalThis.document,
  onSelect = null) {
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
    if (typeof onSelect === 'function') {
      const button = documentSource.createElement('button');
      button.type = 'button';
      button.textContent = 'View published lesson';
      button.addEventListener('click', () => onSelect(assignment.id));
      item.append(button);
    }
    list.append(item);
  }
  target.replaceChildren(list);
}


// Published lesson metadata is not a Native scientific result. Render all
// teacher-authored content as inert text, without HTML or script interpretation.
export function renderLesson(target, state, documentSource = globalThis.document) {
  if (!target || typeof target.replaceChildren !== 'function' ||
      !documentSource || typeof documentSource.createElement !== 'function')
    throw new TypeError('A DOM target and document are required');
  const message = value => {
    const paragraph = documentSource.createElement('p');
    paragraph.textContent = value;
    target.replaceChildren(paragraph);
  };
  if (state?.status === 'no-selection') {
    message('Select a published assignment to view its lesson.');
    return;
  }
  if (state?.status === 'loading') {
    message('Loading published lesson…');
    return;
  }
  if (state?.status === 'unavailable') {
    message('The published lesson could not be loaded. Retry from the assignment list.');
    return;
  }
  if (state?.status !== 'available' || !state.lesson ||
      !Number.isSafeInteger(state.lesson.version) || state.lesson.version < 1 ||
      typeof state.lesson.contentSha256 !== 'string' ||
      !/^[0-9a-f]{64}$/.test(state.lesson.contentSha256) ||
      state.lesson.content == null || Array.isArray(state.lesson.content) ||
      typeof state.lesson.content !== 'object')
    throw new TypeError('A validated published lesson is required');

  const heading = documentSource.createElement('h3');
  heading.textContent = 'Published lesson — version ' + state.lesson.version;
  const provenance = documentSource.createElement('p');
  provenance.textContent = 'Coursework metadata only; not a verified scientific record.';
  const body = documentSource.createElement('pre');
  body.className = 'lesson-content';
  body.textContent = JSON.stringify(state.lesson.content, null, 2);
  target.replaceChildren(heading, provenance, body);
}


// Instructor/admin academic overview is a view of JWT/RLS-returned rows,
// not proof of membership grants and never an editable gradebook.
// All university-supplied strings are inert textContent.
export function renderStaffOverview(target, state, documentSource = globalThis.document) {
  if (!target || typeof target.replaceChildren !== 'function' ||
      !documentSource || typeof documentSource.createElement !== 'function')
    throw new TypeError('A DOM target and document are required');
  const message = value => {
    const paragraph = documentSource.createElement('p');
    paragraph.textContent = value;
    target.replaceChildren(paragraph);
  };
  if (state?.status === 'loading') { message('Loading instructor academic overview…'); return; }
  if (state?.status === 'unavailable') {
    message('Instructor academic overview could not be loaded. Check the connection and retry.');
    return;
  }
  if (state?.status === 'no-staff-context') {
    message('Select an active instructor or university administrator institution.');
    return;
  }
  if (state?.status !== 'available' || !state.overview ||
      !Array.isArray(state.overview.courses) ||
      !Array.isArray(state.overview.sections) ||
      !Array.isArray(state.overview.assignments))
    throw new TypeError('Verified staff overview required');

  const { courses, sections, assignments } = state.overview;
  const fragment = documentSource.createElement('div');
  const advisory = documentSource.createElement('p');
  advisory.textContent =
    'Read-only academic metadata. Up to the first 100 RLS-visible records per category; not a complete university audit.';
  fragment.append(advisory);
  if (courses.length === 0) {
    const empty = documentSource.createElement('p');
    empty.textContent = 'No courses visible for this staff institution.';
    fragment.append(empty);
    target.replaceChildren(fragment);
    return;
  }
  const list = documentSource.createElement('ul');
  list.className = 'staff-course-list';
  for (const course of courses) {
    const item = documentSource.createElement('li');
    const heading = documentSource.createElement('h3');
    heading.textContent = course.code + ' — ' + course.name + ' (' + course.term + ')';
    item.append(heading);
    const courseSections = sections.filter(section => section.courseId === course.id);
    if (courseSections.length === 0) {
      const empty = documentSource.createElement('p');
      empty.textContent = 'No sections visible.';
      item.append(empty);
    } else {
      const sectionList = documentSource.createElement('ul');
      for (const section of courseSections) {
        const sectionItem = documentSource.createElement('li');
        const sectionHeading = documentSource.createElement('h4');
        sectionHeading.textContent = 'Section ' + section.sectionCode;
        sectionItem.append(sectionHeading);
        const inSection = assignments.filter(a => a.sectionId === section.id);
        if (inSection.length === 0) {
          const empty = documentSource.createElement('p');
          empty.textContent = 'No published assignments visible.';
          sectionItem.append(empty);
        } else {
          const assignmentsList = documentSource.createElement('ul');
          for (const a of inSection) {
            const row = documentSource.createElement('li');
            row.textContent = a.title + (a.dueAt
              ? ' — due ' + new Date(a.dueAt).toLocaleString('en', {
                dateStyle: 'medium', timeStyle: 'short' })
              : ' — due date not set');
            assignmentsList.append(row);
          }
          sectionItem.append(assignmentsList);
        }
        sectionList.append(sectionItem);
      }
      item.append(sectionList);
    }
    list.append(item);
  }
  fragment.append(list);
  target.replaceChildren(fragment);
}
