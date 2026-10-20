import test from 'node:test';
import assert from 'node:assert/strict';
import { renderAssignments } from '../src/view.mjs';

class FakeNode {
  constructor(tag) { this.tag = tag; this.children = []; this.textContent = ''; }
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
