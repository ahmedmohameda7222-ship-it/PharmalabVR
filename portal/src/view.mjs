// English-only academic metadata rendering. Never use HTML interpolation.
export function renderAssignments(target, state, documentSource = globalThis.document,
  onSelect = null, onResults = null) {
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
    if (typeof onResults === 'function') {
      const resultsButton = documentSource.createElement('button');
      resultsButton.type = 'button';
      resultsButton.textContent = 'View academic results';
      resultsButton.addEventListener('click', () => onResults(assignment.id));
      item.append(resultsButton);
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
export function renderStaffOverview(target, state, documentSource = globalThis.document,
  onSectionSelect = null, onSubmissionsSelect = null) {
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
        if (typeof onSubmissionsSelect === 'function') {
          const submissionsButton = documentSource.createElement('button');
          submissionsButton.type = 'button';
          submissionsButton.textContent = 'View submissions';
          submissionsButton.addEventListener('click', () => onSubmissionsSelect(section.id));
          sectionItem.append(submissionsButton);
        }
        if (typeof onSectionSelect === 'function' &&
            ['instructor', 'university_admin'].includes(state.overview.role)) {
          const selectButton = documentSource.createElement('button');
          selectButton.type = 'button';
          selectButton.textContent = 'Publish assignment in this section';
          selectButton.addEventListener('click', () => onSectionSelect(section.id));
          sectionItem.append(selectButton);
        }
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


// Uses only already-published server-sourced version references; the callback
// is not authorization. The server RPC checks instructor role, section,
// immutable content pins and idempotent request identity.
export function renderPublicationForm(target, state, documentSource = globalThis.document,
  onPublish = null) {
  if (!target || typeof target.replaceChildren !== 'function' ||
      !documentSource || typeof documentSource.createElement !== 'function')
    throw new TypeError('A DOM target and document are required');
  const message = value => {
    const paragraph = documentSource.createElement('p');
    paragraph.textContent = value;
    target.replaceChildren(paragraph);
  };
  if (state?.status === 'no-selection') {
    message('Select a section from the instructor overview to publish an assignment.');
    return;
  }
  if (state?.status === 'loading') {
    message('Loading published lesson and rubric versions…');
    return;
  }
  if (state?.status === 'unavailable') {
    message('Published lesson and rubric choices could not be loaded.');
    return;
  }
  const options = state?.options;
  if (state?.status !== 'available' || !options ||
      !Array.isArray(options.lessons) || !Array.isArray(options.rubrics))
    throw new TypeError('Published assignment choices required');
  if (options.lessons.length === 0 || options.rubrics.length === 0) {
    message('No published lesson or rubric version is available for this course.');
    return;
  }
  const form = documentSource.createElement('form');
  const introduction = documentSource.createElement('p');
  introduction.textContent =
    'Choose immutable published versions. Publishing is audited by your university server.';
  form.append(introduction);
  const namedInput = (name, label, type) => {
    const elementLabel = documentSource.createElement('label');
    elementLabel.textContent = label;
    const input = documentSource.createElement('input');
    input.name = name;
    input.type = type;
    input.value = '';
    if (name === 'title') { input.required = true; input.maxLength = 200; }
    elementLabel.append(input);
    form.append(elementLabel);
    return input;
  };
  const versionSelect = (name, label, versions) => {
    const elementLabel = documentSource.createElement('label');
    elementLabel.textContent = label;
    const input = documentSource.createElement('select');
    input.name = name;
    for (const version of versions) {
      const option = documentSource.createElement('option');
      option.value = version.id;
      option.textContent = 'Published version ' + version.version + ' — ' + version.id;
      input.append(option);
    }
    input.value = versions[0].id;
    elementLabel.append(input);
    form.append(elementLabel);
    return input;
  };
  const title = namedInput('title', 'Assignment title', 'text');
  const lesson = versionSelect('lessonVersionId', 'Published lesson', options.lessons);
  const rubric = versionSelect('rubricVersionId', 'Published rubric', options.rubrics);
  const due = namedInput('dueAt', 'Due date (optional, local time)', 'datetime-local');
  const submit = documentSource.createElement('button');
  submit.type = 'submit';
  submit.textContent = 'Publish assignment';
  form.append(submit);
  form.addEventListener('submit', event => {
    event.preventDefault();
    if (typeof onPublish !== 'function') return;
    const date = due.value
      ? new Date(due.value).toISOString() : null;
    onPublish({ sectionId: options.sectionId,
      lessonVersionId: lesson.value, rubricVersionId: rubric.value,
      title: title.value, dueAt: date });
  });
  target.replaceChildren(form);
}


// University-admin-only UI affordance; actual permissions, tenancy and
// request idempotency are enforced by existing Supabase RPCs.
export function renderAdminSetup(target, state, documentSource = globalThis.document,
  onCreateCourse = null, onCreateSection = null, onEnrollStudent = null) {
  if (!target || typeof target.replaceChildren !== 'function' ||
      !documentSource || typeof documentSource.createElement !== 'function')
    throw new TypeError('A DOM target and document are required');
  const message = value => {
    const p = documentSource.createElement('p');
    p.textContent = value;
    target.replaceChildren(p);
  };
  if (state?.status === 'no-admin-context') {
    message('Select an active university administrator institution.');
    return;
  }
  if (state?.status === 'loading') {
    message('Loading institution administration…');
    return;
  }
  if (state?.status === 'unavailable') {
    message('Institution administration could not be loaded.');
    return;
  }
  if (state?.status !== 'available' || !Array.isArray(state.courses))
    throw new TypeError('RLS-visible administrator course list required');

  const root = documentSource.createElement('div');
  const warning = documentSource.createElement('p');
  warning.textContent = 'These actions create audited academic metadata only, never Native scientific outcomes.';
  root.append(warning);
  const makeInput = (form, name, labelText, maximum) => {
    const label = documentSource.createElement('label');
    label.textContent = labelText;
    const input = documentSource.createElement('input');
    input.name = name;
    input.type = 'text';
    input.required = true;
    input.maxLength = maximum;
    input.value = '';
    label.append(input);
    form.append(label);
    return input;
  };
  const createCourse = documentSource.createElement('form');
  const courseHeader = documentSource.createElement('h3');
  courseHeader.textContent = 'Create course';
  createCourse.append(courseHeader);
  const code = makeInput(createCourse, 'code', 'Course code (uppercase)', 40);
  const name = makeInput(createCourse, 'name', 'Course name', 200);
  const term = makeInput(createCourse, 'term', 'Term identifier', 32);
  const courseSubmit = documentSource.createElement('button');
  courseSubmit.type = 'submit';
  courseSubmit.textContent = 'Create course';
  createCourse.append(courseSubmit);
  createCourse.addEventListener('submit', event => {
    event.preventDefault();
    if (typeof onCreateCourse === 'function')
      onCreateCourse({ code: code.value, name: name.value, term: term.value });
  });
  root.append(createCourse);

  if (!state.courses.length) {
    const empty = documentSource.createElement('p');
    empty.textContent = 'Create a course first to add a section.';
    root.append(empty);
  } else {
    const createSection = documentSource.createElement('form');
    const sectionHeader = documentSource.createElement('h3');
    sectionHeader.textContent = 'Create section';
    createSection.append(sectionHeader);
    const sectionLabel = documentSource.createElement('label');
    sectionLabel.textContent = 'Parent course';
    const courseSelect = documentSource.createElement('select');
    courseSelect.name = 'courseId';
    for (const course of state.courses) {
      const option = documentSource.createElement('option');
      option.value = course.id;
      option.textContent = course.code + ' — ' + course.name;
      courseSelect.append(option);
    }
    courseSelect.value = state.courses[0].id;
    sectionLabel.append(courseSelect);
    createSection.append(sectionLabel);
    const sectionCode = makeInput(createSection, 'sectionCode',
      'Section code (uppercase)', 20);
    const sectionSubmit = documentSource.createElement('button');
    sectionSubmit.type = 'submit';
    sectionSubmit.textContent = 'Create section';
    createSection.append(sectionSubmit);
    createSection.addEventListener('submit', event => {
      event.preventDefault();
      if (typeof onCreateSection === 'function')
        onCreateSection({ courseId: courseSelect.value,
          sectionCode: sectionCode.value });
    });
    root.append(createSection);
  }
  if (Array.isArray(state.sections) && state.sections.length > 0) {
    const enrollForm = documentSource.createElement('form');
    const enrollHeader = documentSource.createElement('h3');
    enrollHeader.textContent = 'Enroll verified existing student';
    enrollForm.append(enrollHeader);
    const sectionLabel = documentSource.createElement('label');
    sectionLabel.textContent = 'Authorized section';
    const sectionSelect = documentSource.createElement('select');
    sectionSelect.name = 'sectionId';
    for (const section of state.sections) {
      const option = documentSource.createElement('option');
      option.value = section.id;
      option.textContent = section.sectionCode + ' — ' + section.courseId;
      sectionSelect.append(option);
    }
    sectionSelect.value = state.sections[0].id;
    sectionLabel.append(sectionSelect);
    enrollForm.append(sectionLabel);
    const studentId = makeInput(enrollForm, 'studentId',
      'Existing verified student user UUID', 36);
    const enrollmentButton = documentSource.createElement('button');
    enrollmentButton.type = 'submit';
    enrollmentButton.textContent = 'Enroll student';
    enrollForm.append(enrollmentButton);
    enrollForm.addEventListener('submit', event => {
      event.preventDefault();
      if (typeof onEnrollStudent === 'function')
        onEnrollStudent({ sectionId: sectionSelect.value,
          studentId: studentId.value });
    });
    root.append(enrollForm);
  }
  target.replaceChildren(root);
}


// Read-only, server-authorized academic outcomes. Approved grades come from
// RLS-protected grades; feedback must be explicitly released by an instructor.
// Neither this UI nor a client digest authenticates Native scientific origin.
export function renderStudentResults(target, state, documentSource = globalThis.document) {
  if (!target || typeof target.replaceChildren !== 'function' ||
      !documentSource || typeof documentSource.createElement !== 'function')
    throw new TypeError('A DOM target and document are required');
  const message = value => {
    const p = documentSource.createElement('p');
    p.textContent = value;
    target.replaceChildren(p);
  };
  if (state?.status === 'no-selection') {
    message('Select a published assignment to view academic results.');
    return;
  }
  if (state?.status === 'loading') {
    message('Loading student academic results…');
    return;
  }
  if (state?.status === 'unavailable') {
    message('Academic results could not be loaded. Check the connection and retry.');
    return;
  }
  const results = state?.results;
  if (state?.status !== 'available' || !results ||
      !Array.isArray(results.submissions) ||
      !Array.isArray(results.grades) || !Array.isArray(results.feedback))
    throw new TypeError('Validated student academic results required');
  if (results.submissions.length === 0) {
    message('No submitted attempt is visible for this published assignment.');
    return;
  }
  const root = documentSource.createElement('div');
  const caution = documentSource.createElement('p');
  caution.textContent = 'Academic metadata from the university server. Native scientific origin is not attested by this view.';
  root.append(caution);
  const list = documentSource.createElement('ul');
  for (const sub of results.submissions) {
    const item = documentSource.createElement('li');
    const heading = documentSource.createElement('h3');
    heading.textContent = 'Submitted attempt ' + sub.id;
    item.append(heading);
    const grade = results.grades.find(g => g.submissionId === sub.id);
    const summary = documentSource.createElement('p');
    summary.textContent = grade
      ? 'Approved academic grade: ' + grade.score + '%'
      : 'No approved academic grade is available.';
    item.append(summary);
    const visible = results.feedback.filter(x => x.submissionId === sub.id);
    if (!visible.length) {
      const empty = documentSource.createElement('p');
      empty.textContent = 'No released instructor feedback is available.';
      item.append(empty);
    } else {
      const notes = documentSource.createElement('ul');
      for (const entry of visible) {
        const note = documentSource.createElement('li');
        note.textContent = entry.body;
        notes.append(note);
      }
      item.append(notes);
    }
    list.append(item);
  }
  root.append(list);
  target.replaceChildren(root);
}

// Staff review is a preview of RLS-visible submitted academic metadata only.
// It does not display private evidence bytes, approve grades or promote staged
// unverified artifacts. The UI role never substitutes for PostgreSQL policy.
export function renderStaffSubmissions(target, state, documentSource = globalThis.document,
  onFeedback = null) {
  if (!target || typeof target.replaceChildren !== 'function' ||
      !documentSource || typeof documentSource.createElement !== 'function')
    throw new TypeError('A DOM target and document are required');
  const message = value => {
    const p = documentSource.createElement('p');
    p.textContent = value;
    target.replaceChildren(p);
  };
  if (state?.status === 'no-selection') {
    message('Select a staff-visible section to review submissions.');
    return;
  }
  if (state?.status === 'loading') {
    message('Loading section submissions…');
    return;
  }
  if (state?.status === 'unavailable') {
    message('Section submissions could not be loaded. Check the connection and retry.');
    return;
  }
  if (state?.status !== 'available' || !Array.isArray(state.submissions))
    throw new TypeError('Validated staff submission rows required');
  if (!state.submissions.length) {
    message('No submissions are visible in this section.');
    return;
  }
  const root = documentSource.createElement('div');
  const caution = documentSource.createElement('p');
  caution.textContent =
    'Read-only first 100 RLS-visible submissions. No scientific evidence validation or grade approval.';
  root.append(caution);
  const list = documentSource.createElement('ul');
  for (const sub of state.submissions) {
    const item = documentSource.createElement('li');
    item.textContent = 'Submission ' + sub.id +
      ' — student ' + sub.studentId +
      ' — assignment ' + sub.assignmentId;
    if (typeof onFeedback === 'function' &&
        ['instructor', 'university_admin'].includes(state.role)) {
      const button = documentSource.createElement('button');
      button.type = 'button';
      button.textContent = 'Record instructor feedback';
      button.addEventListener('click', () => onFeedback(sub.id));
      item.append(button);
    }
    list.append(item);
  }
  root.append(list);
  target.replaceChildren(root);
}


// Feedback is append-only on the academic server and defaults PRIVATE.
// The visible-to-student choice is explicit and unselected by default.
// Never render course or feedback text as executable HTML.
export function renderFeedbackForm(target, state, documentSource = globalThis.document,
  onSubmit = null) {
  if (!target || typeof target.replaceChildren !== 'function' ||
      !documentSource || typeof documentSource.createElement !== 'function')
    throw new TypeError('A DOM target and document are required');
  const message = value => {
    const p = documentSource.createElement('p');
    p.textContent = value;
    target.replaceChildren(p);
  };
  if (state?.status === 'no-selection') {
    message('Select a submission from the staff section preview to record feedback.');
    return;
  }
  if (state?.status === 'loading') {
    message('Loading feedback options…');
    return;
  }
  if (state?.status === 'unavailable') {
    message('Instructor feedback form could not be loaded.');
    return;
  }
  if (state?.status !== 'available' ||
      typeof state.submissionId !== 'string' || !state.submissionId)
    throw new TypeError('Selected submission required for instructor feedback');

  const form = documentSource.createElement('form');
  const context = documentSource.createElement('p');
  context.textContent = 'Append-only instructor feedback for submission ' +
    state.submissionId + '. This does not verify Native evidence or approve a grade.';
  form.append(context);
  const label = documentSource.createElement('label');
  label.textContent = 'Feedback text';
  const body = documentSource.createElement('textarea');
  body.name = 'body';
  body.required = true;
  body.maxLength = 12000;
  body.value = '';
  body.rows = 6;
  label.append(body);
  form.append(label);
  const releaseLabel = documentSource.createElement('label');
  releaseLabel.textContent = 'Release this feedback to the student (unchecked means private)';
  const release = documentSource.createElement('input');
  release.name = 'studentVisible';
  release.type = 'checkbox';
  release.checked = false;
  releaseLabel.append(release);
  form.append(releaseLabel);
  const button = documentSource.createElement('button');
  button.type = 'submit';
  button.textContent = 'Record immutable feedback';
  form.append(button);
  form.addEventListener('submit', event => {
    event.preventDefault();
    if (typeof onSubmit !== 'function' ||
        !body.value.trim() || body.value.length > 12000)
      return;
    onSubmit({ submissionId: state.submissionId,
      body: body.value, studentVisible: release.checked === true });
  });
  target.replaceChildren(form);
}
