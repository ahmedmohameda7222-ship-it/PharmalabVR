import test from 'node:test';
import assert from 'node:assert/strict';
import { renderAssignments, renderLesson, renderStaffOverview, renderPublicationForm, renderAdminSetup } from '../src/view.mjs';

class FakeNode {
  constructor(tag) { this.tag = tag; this.children = []; this.textContent = ''; this.events = {}; }
  addEventListener(name, fn) { this.events[name] = fn; }
  replaceChildren(...nodes) { this.children = nodes; this.textContent = ''; }
  append(...nodes) { this.children.push(...nodes); }
  set innerHTML(_) { throw new Error('Unsafe innerHTML injection'); }
}
const document = { createElement: tag => new FakeNode(tag) };
function visibleText(node) {
  return [node.textContent, ...node.children.map(visibleText)].join(' ');
}
const assignment = {
  title: '<script>alert(1)</script>', id: 'a',
  publishedAt: '2026-10-20T02:00:00Z', dueAt: null
};

test('verified empty assignments are distinct from unavailable or expired context', () => {
  const root = new FakeNode('section');
  renderAssignments(root, { status: 'available', assignments: [] }, document);
  assert.match(visibleText(root), /no published assignments/i);
  renderAssignments(root, { status: 'unavailable', assignments: [] }, document);
  assert.match(visibleText(root), /could not be loaded/i);
  assert.doesNotMatch(visibleText(root), /no published assignments/i);
  renderAssignments(root, { status: 'no-student-context', assignments: [] }, document);
  assert.match(visibleText(root), /select an active student institution/i);
  assert.doesNotMatch(visibleText(root), /no published assignments/i);
});

test('published assignments use safe text nodes, never HTML insertion', () => {
  const root = new FakeNode('section');
  renderAssignments(root, { status: 'available', assignments: [assignment] }, document);
  assert.match(visibleText(root), /<script>alert\(1\)<\/script>/);
  assert.match(visibleText(root), /due date not set/i);
  assert.equal(root.children.length, 1);
  assert.equal(root.children[0].tag, 'ul');
});

test('network loading state cannot impersonate academic data', () => {
  const root = new FakeNode('section');
  renderAssignments(root, { status: 'loading', assignments: [assignment] }, document);
  assert.match(visibleText(root), /loading/i);
  assert.doesNotMatch(visibleText(root), /script>/);
});

test('student can select a published assignment without auto-opening a lesson', () => {
  const root = new FakeNode('section');
  const selected = [];
  renderAssignments(root, { status: 'available', assignments: [assignment] }, document,
    id => selected.push(id));
  const card = root.children[0].children[0];
  const selectButton = card.children.at(-1);
  assert.equal(selectButton.tag, 'button');
  assert.equal(selected.length, 0);
  selectButton.events.click();
  assert.deepEqual(selected, ['a']);
});

test('lesson content is inert text and preserves declared server provenance', () => {
  const root = new FakeNode('section');
  renderLesson(root, { status: 'available', lesson: {
    id: 'lesson-one', version: 3, contentSha256: 'a'.repeat(64),
    content: { title: '<img src=x onerror=alert(1)>', steps: ['Read instructions'] }
  } }, document);
  assert.match(visibleText(root), /version 3/i);
  assert.match(visibleText(root), /<img src=x onerror=alert\(1\)>/);
  assert.match(visibleText(root), /not a verified scientific record/i);
});

test('failed lesson read never leaves previous lesson contents visible', () => {
  const root = new FakeNode('section');
  renderLesson(root, { status: 'available', lesson: {
    version: 1, contentSha256: 'b'.repeat(64), content: { title: 'Private lesson' }
  } }, document);
  renderLesson(root, { status: 'unavailable' }, document);
  assert.match(visibleText(root), /could not be loaded/i);
  assert.doesNotMatch(visibleText(root), /Private lesson/);
  renderLesson(root, { status: 'no-selection' }, document);
  assert.match(visibleText(root), /select a published assignment/i);
});

test('staff overview presents tenant-scoped course, section and published assignment tree', () => {
  const root = new FakeNode('section');
  renderStaffOverview(root, { status: 'available', overview: {
    role: 'instructor',
    courses: [{ id: 'course-1', code: 'CHEM101',
      name: '<svg onload=alert(1)>', term: 'Fall 2026' }],
    sections: [{ id: 'section-1', courseId: 'course-1',
      sectionCode: 'LAB01' }],
    assignments: [{ id: 'assignment-1', sectionId: 'section-1',
      courseId: 'course-1', title: '<script>unsafe</script>',
      publishedAt: '2026-10-20T02:00:00Z', dueAt: null }]
  } }, document);
  const value = visibleText(root);
  assert.match(value, /CHEM101/);
  assert.match(value, /LAB01/);
  assert.match(value, /<svg onload=alert\(1\)>/);
  assert.match(value, /<script>unsafe<\/script>/);
  assert.match(value, /first 100/i);
  assert.match(value, /read.only/i);
});

test('staff failures clear previously rendered academic content', () => {
  const root = new FakeNode('section');
  renderStaffOverview(root, { status: 'available', overview: {
    role: 'university_admin',
    courses: [{ id: 'course-1', code: 'CHEM', name: 'Private course', term: 'Fall' }],
    sections: [], assignments: []
  } }, document);
  renderStaffOverview(root, { status: 'unavailable' }, document);
  assert.match(visibleText(root), /could not be loaded/i);
  assert.doesNotMatch(visibleText(root), /Private course/);
  renderStaffOverview(root, { status: 'no-staff-context' }, document);
  assert.match(visibleText(root), /select an active instructor/i);
  assert.doesNotMatch(visibleText(root), /Private course/);
});

test('empty verified staff results are distinct from unavailable state', () => {
  const root = new FakeNode('section');
  renderStaffOverview(root, { status: 'available', overview: {
    role: 'instructor', courses: [], sections: [], assignments: []
  } }, document);
  assert.match(visibleText(root), /no courses visible/i);
  assert.doesNotMatch(visibleText(root), /could not be loaded/i);
});

test('instructor must explicitly select a section before publishing', () => {
  const root = new FakeNode('section');
  const seen = [];
  renderStaffOverview(root, { status: 'available', overview: {
    role: 'instructor', courses: [{ id: 'course-1', code: 'CHEM101',
      name: 'Foundations', term: 'Fall' }],
    sections: [{ id: 'section-1', courseId: 'course-1', sectionCode: 'LAB01' }],
    assignments: []
  } }, document, sectionId => seen.push(sectionId));
  const findButtons = node => [node, ...node.children.flatMap(findButtons)]
    .filter(n => n.tag === 'button');
  const buttons = findButtons(root);
  assert.equal(buttons.length, 1);
  assert.equal(seen.length, 0);
  buttons[0].events.click();
  assert.deepEqual(seen, ['section-1']);
});

test('published version form submits explicit validated immutable version choices', () => {
  const root = new FakeNode('section');
  const received = [];
  renderPublicationForm(root, { status: 'available', options: {
    sectionId: 'section-1', courseId: 'course-1',
    lessons: [{ id: 'lesson-1', version: 3 }],
    rubrics: [{ id: 'rubric-2', version: 2 }]
  } }, document, draft => received.push(draft));
  const find = node => [node, ...node.children.flatMap(find)];
  const form = find(root).find(n => n.tag === 'form');
  assert.ok(form);
  const inputs = find(form).filter(n => ['input', 'select'].includes(n.tag));
  const title = inputs.find(x => x.name === 'title');
  assert.ok(title);
  title.value = 'CHEM101 lab';
  const event = { preventDefault() {} };
  form.events.submit(event);
  assert.equal(received.length, 1);
  assert.equal(received[0].sectionId, 'section-1');
  assert.equal(received[0].lessonVersionId, 'lesson-1');
  assert.equal(received[0].rubricVersionId, 'rubric-2');
  assert.equal(received[0].title, 'CHEM101 lab');
});

test('publication form never claims successful assignment after failed read', () => {
  const root = new FakeNode('section');
  renderPublicationForm(root, { status: 'unavailable' }, document);
  assert.match(visibleText(root), /could not be loaded/i);
  renderPublicationForm(root, { status: 'no-selection' }, document);
  assert.match(visibleText(root), /select a section/i);
});

test('admin can create first course before any section exists', () => {
  const root = new FakeNode('section');
  const submitted = [];
  renderAdminSetup(root, { status: 'available', courses: [] }, document,
    x => submitted.push(x));
  const walk = n => [n, ...n.children.flatMap(walk)];
  const forms = walk(root).filter(n => n.tag === 'form');
  assert.equal(forms.length, 1);
  for (const input of walk(forms[0]).filter(n => n.tag === 'input'))
    input.value = input.name === 'code' ? 'CHEM101' :
      input.name === 'term' ? 'Fall2026' : 'Laboratory chemistry';
  forms[0].events.submit({ preventDefault() {} });
  assert.deepEqual(submitted[0], {
    code: 'CHEM101', name: 'Laboratory chemistry', term: 'Fall2026'
  });
  assert.match(visibleText(root), /create a course first/i);
});

test('admin section creation pins one visible institution course', () => {
  const root = new FakeNode('section');
  const sent = [];
  renderAdminSetup(root, { status: 'available', courses: [
    { id: 'course-1', name: 'Chemistry', code: 'CHEM101', term: 'Fall2026' }
  ] }, document, null, x => sent.push(x));
  const walk = n => [n, ...n.children.flatMap(walk)];
  const forms = walk(root).filter(n => n.tag === 'form');
  assert.equal(forms.length, 2);
  const sectionForm = forms[1];
  const code = walk(sectionForm).find(n => n.tag === 'input');
  code.value = 'LAB02';
  sectionForm.events.submit({ preventDefault() {} });
  assert.deepEqual(sent[0], { courseId: 'course-1', sectionCode: 'LAB02' });
});

test('administrator enrollment form selects existing section and verified student UUID', () => {
  const root = new FakeNode('section');
  const received = [];
  renderAdminSetup(root, { status: 'available', courses: [
    { id: 'course-1', code: 'CHEM101', name: 'Foundations', term: 'Fall' }
  ], sections: [
    { id: 'section-1', courseId: 'course-1', sectionCode: 'LAB01' }
  ] }, document, null, null, value => received.push(value));
  const walk = n => [n, ...n.children.flatMap(walk)];
  const forms = walk(root).filter(n => n.tag === 'form');
  assert.equal(forms.length, 3);
  const form = forms[2];
  const studentId = walk(form).find(n => n.tag === 'input');
  studentId.value = '11111111-1111-4111-8111-111111111111';
  form.events.submit({ preventDefault() {} });
  assert.deepEqual(received[0], {
    sectionId: 'section-1', studentId: studentId.value
  });
});
