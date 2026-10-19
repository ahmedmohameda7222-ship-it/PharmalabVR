import test from "node:test";
import assert from "node:assert/strict";
import { verifyExistingStaging, isDuplicateStagingUpload } from "./reconcile.mjs";

const tenant="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const student="44444444-4444-4444-8444-444444444444";
const session="aaaa0000-0000-4000-8000-000000000051";
const artifact="aaaa0000-0000-4000-8000-000000000082";
const bytes=new TextEncoder().encode('{"sample":3}');
const hash=Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256",bytes)))
  .map(x=>x.toString(16).padStart(2,"0")).join("");
const row={
 id:artifact,institution_id:tenant,lab_session_id:session,student_id:student,
 object_key:tenant+"/"+artifact,size_bytes:bytes.length,
 sha256:hash,content_type:"application/json"
};
const input={row,tenantId:tenant,artifactId:artifact,sessionId:session,
 studentId:student,sha256:hash,bytes};

test("U04 exact retry confirms immutable metadata and actual stored JSON bytes",async()=>{
 assert.deepEqual(await verifyExistingStaging(input),{
  artifactId:artifact,sha256:hash,alreadyStaged:true
 });
});

test("U04 exact retry rejects a foreign student, tenant, session or artifact",async()=>{
 const foreign="bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
 for(const patch of [
  {studentId:foreign},{sessionId:foreign},{tenantId:foreign},{artifactId:foreign},
  {row:{...row,student_id:foreign}},{row:{...row,institution_id:foreign}},
  {row:{...row,lab_session_id:foreign}},{row:{...row,id:foreign}},
  {row:{...row,object_key:"../evidence"}}, {row:null}
 ]){
  await assert.rejects(verifyExistingStaging({...input,...patch}));
 }
});

test("U04 retry never trusts metadata alone if Storage bytes differ or disappeared",async()=>{
 await assert.rejects(verifyExistingStaging({...input,bytes:new TextEncoder().encode('{"sample":4}')}));
 await assert.rejects(verifyExistingStaging({...input,bytes:new Uint8Array(0)}));
 await assert.rejects(verifyExistingStaging({...input,bytes:null}));
 await assert.rejects(verifyExistingStaging({...input,row:{...row,sha256:"0".repeat(64)}}));
 await assert.rejects(verifyExistingStaging({...input,row:{...row,size_bytes:1234}}));
 await assert.rejects(verifyExistingStaging({...input,row:{...row,content_type:"text/html"}}));
});

test("U04 recognizes only documented duplicate upload error codes",()=>{
 for(const error of [
  {statusCode:"409",error:"ResourceAlreadyExists"},
  {statusCode:409,error:"KeyAlreadyExists"},
  {statusCode:"400",error:"Duplicate"},
  {statusCode:"400",error:"KeyAlreadyExists"},
  // Storage standard uploads return HTTP 400 "Asset Already Exists".
  {statusCode:"400",code:"AssetAlreadyExists",error:"Asset Already Exists"},
  {statusCode:400,error:"Asset Already Exists"},
  {statusCode:"409",error:"The resource already exists",code:"ResourceAlreadyExists"},
  {statusCode:409,error:"Storage API says duplicate",code:"KeyAlreadyExists"}
 ])assert.equal(isDuplicateStagingUpload(error),true);
 for(const error of [null,{}, {statusCode:400,error:"InvalidMimeType"},
   {statusCode:401,error:"Unauthorized"},{statusCode:503,error:"InternalError"}])
    assert.equal(isDuplicateStagingUpload(error),false);
});
