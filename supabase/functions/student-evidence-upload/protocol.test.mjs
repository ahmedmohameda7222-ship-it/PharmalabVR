import test from "node:test";
import assert from "node:assert/strict";
import { readBoundedEvidence, parseUploadHeaders } from "./protocol.mjs";

const A="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const S="aaaa0000-0000-4000-8000-000000000051";
const E="aaaa0000-0000-4000-8000-000000000082";
const H="a".repeat(64);
function headers(extra={}){return new Headers({
 "content-type":"application/json","x-institution-id":A,
 "x-session-id":S,"x-artifact-id":E,"x-evidence-sha256":H,...extra});}
function request(bytes,overrides={},length){
 const h=headers(overrides);
 if(length!==undefined)h.set("content-length",String(length));
 return new Request("https://unit.local/upload",{method:"POST",headers:h,body:bytes});
}
test("U04 upload HTTP requires canonical tenant, session and artifact IDs",()=>{
 const m=parseUploadHeaders(headers());
 assert.deepEqual(m,{tenantId:A,sessionId:S,artifactId:E,sha256:H});
 for(const patch of [
   {"x-institution-id":"../storage"},{"x-artifact-id":E.toUpperCase()},
   {"x-session-id":""},{"x-evidence-sha256":"0".repeat(63)},
   {"content-type":"text/html"}])
     assert.throws(()=>parseUploadHeaders(headers(patch)));
});
test("U04 accepts the actual small JSON export bytes",async()=>{
 const body=new TextEncoder().encode('{"native":true}');
 const result=await readBoundedEvidence(request(body));
 assert.deepEqual(result,body);
});
test("U04 rejects declared oversized body even if small bytes follow",async()=>{
 await assert.rejects(readBoundedEvidence(request(
   new TextEncoder().encode('{"native":true}'),{},1048577)));
});
test("U04 rejects a chunked body over the limit with no trustworthy content-length",async()=>{
 const source=new Uint8Array(1048577).fill(65);
 const stream=new ReadableStream({start(c){c.enqueue(source);c.close();}});
 const req=new Request("https://unit.local/upload",{method:"POST",
   headers:headers(),body:stream,duplex:"half"});
 await assert.rejects(readBoundedEvidence(req));
});
test("U04 rejects empty uploads",async()=>{
 await assert.rejects(readBoundedEvidence(request(new Uint8Array(0))));
});
