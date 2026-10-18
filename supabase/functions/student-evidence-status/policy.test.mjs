import test from "node:test";
import assert from "node:assert/strict";
import { assertStudentEvidenceAccess, isCanonicalTenant } from "./policy.mjs";

const A = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const B = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
const user = { id:"11111111-1111-4111-8111-111111111111", email_confirmed_at:"2026-10-18T00:00:00Z" };
const membership = { institution_id:A,user_id:user.id,role:"student",status:"active" };
const institution = { id:A,status:"active" };
test("U04 status access requires verified active student and identical tenant", () => {
  assert.equal(assertStudentEvidenceAccess(A,user,membership,institution),A);
  assert.equal(isCanonicalTenant(A),true);
});
test("U04 rejects forged/malformed tenant IDs", () => {
  assert.equal(isCanonicalTenant("../../../storage"),false);
  assert.equal(isCanonicalTenant(A.toUpperCase()),false);
});
test("U04 rejects foreign institution membership", () => {
  assert.throws(() => assertStudentEvidenceAccess(B,user,membership,institution));
});
test("U04 rejects revoked and invited memberships", () => {
  for(const status of ["revoked","invited"]){
    assert.throws(() => assertStudentEvidenceAccess(A,user,{...membership,status},institution));
  }
});
test("U04 rejects users without confirmed email", () => {
  assert.throws(() => assertStudentEvidenceAccess(A,{...user,email_confirmed_at:null},membership,institution));
});
test("U04 denies nonstudents and foreign account", () => {
  assert.throws(() => assertStudentEvidenceAccess(A,user,{...membership,role:"instructor"},institution));
  assert.throws(() => assertStudentEvidenceAccess(A,{...user,id:"22222222-2222-4222-8222-222222222222"},membership,institution));
});
test("U04 rejects suspended university", () => {
  assert.throws(() => assertStudentEvidenceAccess(A,user,membership,{...institution,status:"suspended"}));
});
