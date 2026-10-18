import test from "node:test";
import assert from "node:assert/strict";
import { checkUploadAdmission, verifyEvidenceBytes, evidenceObjectKey } from "./intent.mjs";

const A="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const B="bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
const student="44444444-4444-4444-8444-444444444444";
const sessionId="aaaa0000-0000-4000-8000-000000000051";
const artifact="aaaa0000-0000-4000-8000-000000000082";
const digest="00".repeat(32);
const input={
 tenantId:A,artifactId:artifact,sessionId,user:{id:student,email_confirmed_at:"2026-10-18T00:00:00Z"},
 membership:{institution_id:A,user_id:student,role:"student",status:"active"},
 institution:{id:A,status:"active"},
 session:{id:sessionId,institution_id:A,user_id:student,sync_state:"pending"},
 sha256:digest,contentType:"application/json",sizeBytes:12
};
test("U04 accepts a verified active student and owned same-tenant Native session",()=>{
 assert.equal(checkUploadAdmission(input).artifactId,artifact);
 assert.equal(evidenceObjectKey(A,artifact),A+"/"+artifact);
});
test("U04 refuses forged tenant or artifact key",()=>{
 for(const patch of [{tenantId:B},{artifactId:"../secrets"},{artifactId:artifact.toUpperCase()}])
  assert.throws(()=>checkUploadAdmission({...input,...patch}));
});
test("U04 rejects suspended tenant, revoked student and unverified Auth",()=>{
 assert.throws(()=>checkUploadAdmission({...input,institution:{...input.institution,status:"suspended"}}));
 assert.throws(()=>checkUploadAdmission({...input,membership:{...input.membership,status:"revoked"}}));
 assert.throws(()=>checkUploadAdmission({...input,user:{...input.user,email_confirmed_at:null}}));
});
test("U04 does not let staff or another user stage student evidence",()=>{
 assert.throws(()=>checkUploadAdmission({...input,membership:{...input.membership,role:"instructor"}}));
 assert.throws(()=>checkUploadAdmission({...input,user:{...input.user,id:"11111111-1111-4111-8111-111111111111"}}));
});
test("U04 refuses unrelated, conflicted and foreign Native sessions",()=>{
 for(const session of [
  {...input.session,user_id:"22222222-2222-4222-8222-222222222222"},
  {...input.session,institution_id:B},
  {...input.session,sync_state:"conflict"},
  null
 ])assert.throws(()=>checkUploadAdmission({...input,session}));
});
test("U04 denies excessive bytes, unknown MIME, and malformed declared digest",()=>{
 for(const change of [{sizeBytes:0},{sizeBytes:1048577},{contentType:"text/html"},
                       {sha256:"a".repeat(63)},{sha256:"A".repeat(64)}])
   assert.throws(()=>checkUploadAdmission({...input,...change}));
});
test("U04 digest must be computed from actual bytes and equals declared length",async()=>{
 const bytes=new TextEncoder().encode('{"data":42}');
 const actual=await crypto.subtle.digest("SHA-256",bytes);
 const sha=Array.from(new Uint8Array(actual)).map(x=>x.toString(16).padStart(2,"0")).join("");
 await assert.doesNotReject(verifyEvidenceBytes(bytes,sha,bytes.length));
 await assert.rejects(verifyEvidenceBytes(bytes,"0".repeat(64),bytes.length));
 await assert.rejects(verifyEvidenceBytes(bytes,sha,bytes.length+1));
 await assert.rejects(verifyEvidenceBytes(new Uint8Array(0),sha,0));
});
test("U04 forbids missing or forged artifact scope",()=>{
 assert.throws(()=>checkUploadAdmission({...input,sessionId:B}));
 assert.throws(()=>checkUploadAdmission({...input,membership:{...input.membership,institution_id:B}}));
});
