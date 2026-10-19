// Isolated local-only integration: real GoTrue -> REST RPC -> Edge -> Storage.
// DO NOT use against an owner project. No production identifiers/keys here.
import assert from "node:assert/strict";
import { createHash,randomUUID } from "node:crypto";

const api = process.env.API_URL?.replace(/\/$/,"");
const anon = process.env.ANON_KEY;
const service = process.env.SERVICE_ROLE_KEY;
if (!api || !api.startsWith("http://127.0.0.1:54321") || !anon || !service)
  throw new Error("U04 local HTTP smoke requires localhost Supabase CLI keys only");
const tenant="aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const pw="Disposable-PhaRma-Lab-Tests-2026!";
const secretHeaders = {
  "apikey": service, "Authorization": "Bearer "+service,
  "Content-Type": "application/json"
};
const studentHeaders = token => ({
  "apikey": anon, "Authorization": "Bearer "+token,
  "Content-Type": "application/json"
});
const jsonRequest = async (path,opts={},statuses=[200])=>{
  const r=await fetch(api+path,opts);
  const body=await r.text();
  let data;
  try{data=body?JSON.parse(body):null;}catch{data={error:body.slice(0,220)};}
  assert(statuses.includes(r.status),path+" got HTTP "+r.status+"; "+JSON.stringify(data).slice(0,400));
  return {status:r.status,data};
};
async function ready(){
  for(let i=0;i<50;i++){
    try{
      const r=await fetch(api+"/functions/v1/student-evidence-upload",
        {method:"GET",headers:{"apikey":anon},signal:AbortSignal.timeout(1800)});
      if(r.status!==404 && r.status!==502 && r.status!==503){await r.arrayBuffer();return;}
    }catch{}
    await new Promise(r=>setTimeout(r,1500));
  }
  throw new Error("Local Edge Function never became reachable");
}
async function addUser(address){
  const {data}=await jsonRequest("/auth/v1/admin/users",{
    method:"POST",headers:secretHeaders,
    body:JSON.stringify({email:address,password:pw,email_confirm:true})
  },[200,201]);
  assert.match(data.id,/^[a-f0-9-]{36}$/);
  return data.id;
}
async function signIn(email){
  const {data}=await jsonRequest("/auth/v1/token?grant_type=password",{
    method:"POST",headers:{"apikey":anon,"Content-Type":"application/json"},
    body:JSON.stringify({email,password:pw})
  });
  assert(data.access_token && data.token_type==="bearer");
  return data.access_token;
}
async function adminInsert(table,row){
  const {status}=await jsonRequest("/rest/v1/"+table,{
    method:"POST",headers:{...secretHeaders,Prefer:"return=minimal"},
    body:JSON.stringify(row)
  },[201]);
  assert.equal(status,201);
}
async function stage(token,session,artifact,bytes,sha){
  const headers={
    ...studentHeaders(token),
    "x-institution-id":tenant, "x-session-id":session,
    "x-artifact-id":artifact, "x-evidence-sha256":sha
  };
  return jsonRequest("/functions/v1/student-evidence-upload",
    {method:"POST",headers,body:bytes},[200,201,403,409,422,503]);
}
async function main(){
  await ready();
  const emailA="u04-alpha@pharmalabvr.invalid";
  const emailB="u04-beta@pharmalabvr.invalid";
  const a=await addUser(emailA);
  const b=await addUser(emailB);
  await adminInsert("institutions",{id:tenant,slug:"u04-local-smoke",name:"U04 Local Integration University",status:"active"});
  for(const id of [a,b]){
    await adminInsert("institution_memberships",{institution_id:tenant,user_id:id,role:"student",status:"active"});
  }
  const token=await signIn(emailA);
  const foreignToken=await signIn(emailB);
  const {data:sessionId}=await jsonRequest(
    "/rest/v1/rpc/university_register_staging_session",{
      method:"POST",headers:studentHeaders(token),
      body:JSON.stringify({
        p_institution_id:tenant,p_local_session_id:"u04-http-smoke",
        p_native_branch_id:"local-branch-a",
        p_science_package_sha256:"a".repeat(64),p_request_id:randomUUID()
      })
    },[200]);
  assert.match(sessionId,/^[a-f0-9-]{36}$/);
  const artifact=randomUUID();
  const bytes=Buffer.from('{"native":"untrusted","points":3}');
  const sha=createHash("sha256").update(bytes).digest("hex");
  const first=await stage(token,sessionId,artifact,bytes,sha);
  assert.equal(first.status,201,"First authorized upload must enter quarantine");
  assert.equal(first.data.state,"staged_unverified");
  assert.equal(first.data.artifactId,artifact);
  assert.equal(first.data.sha256,sha);
  const repeated=await stage(token,sessionId,artifact,bytes,sha);
  assert.equal(repeated.status,200,"Exact retransmission must reconcile private bytes; response="+JSON.stringify(repeated.data));
  assert.equal(repeated.data.alreadyStaged,true);
  assert.equal(repeated.data.state,"staged_unverified");
  const changed=Buffer.from('{"native":"altered","points":3}');
  const changedSha=createHash("sha256").update(changed).digest("hex");
  const collision=await stage(token,sessionId,artifact,changed,changedSha);
  assert.equal(collision.status,409,"Conflicting staged bytes must never overwrite");
  const cross=await stage(foreignToken,sessionId,randomUUID(),bytes,sha);
  assert.equal(cross.status,403,"Different student must not stage on A session");
  const badHash=await stage(token,sessionId,randomUUID(),bytes,"0".repeat(64));
  assert.equal(badHash.status,422,"Declared digest must match the real bytes");
  // Two concurrent claims for one artifact: storage must never overwrite,
  // staging metadata must bind to the sole winner and the loser must conflict.
  const racingArtifact=randomUUID();
  const firstBytes=Buffer.from('{"race":"candidate-a"}');
  const secondBytes=Buffer.from('{"race":"candidate-b"}');
  const firstHash=createHash("sha256").update(firstBytes).digest("hex");
  const secondHash=createHash("sha256").update(secondBytes).digest("hex");
  const [raceA,raceB]=await Promise.all([
    stage(token,sessionId,racingArtifact,firstBytes,firstHash),
    stage(token,sessionId,racingArtifact,secondBytes,secondHash)
  ]);
  assert.deepEqual([raceA.status,raceB.status].sort((x,y)=>x-y),[201,409],
    "Concurrent conflicting writes must have one winner and one conflict: "
    +JSON.stringify([raceA.data,raceB.data]));
  const winningBytes=raceA.status===201?firstBytes:secondBytes;
  const winningHash=raceA.status===201?firstHash:secondHash;
  const losingBytes=raceA.status===201?secondBytes:firstBytes;
  const losingHash=raceA.status===201?secondHash:firstHash;
  const raceRetry=await stage(token,sessionId,racingArtifact,winningBytes,winningHash);
  assert.equal(raceRetry.status,200,"Committed concurrent winner must reconcile");
  const loserRetry=await stage(token,sessionId,racingArtifact,losingBytes,losingHash);
  assert.equal(loserRetry.status,409,"Conflicting loser must remain rejected");
  const object=await fetch(api+"/storage/v1/object/authenticated/"
    +"plv-university-evidence-staging/"+tenant+"/"+racingArtifact,{
    headers:{"apikey":service,"Authorization":"Bearer "+service}
  });
  assert.equal(object.status,200,"Private object unavailable to server verification");
  assert.deepEqual(Buffer.from(await object.arrayBuffer()),winningBytes,
    "Racing loser replaced original private Storage object");
  const adminRows=await jsonRequest(
    "/rest/v1/evidence_upload_staging?select=id,institution_id,lab_session_id,student_id,sha256",
    {headers:secretHeaders});
  assert.equal(adminRows.data.length,2);
  assert.equal(adminRows.data[0].student_id,a);
  assert.equal(adminRows.data[0].lab_session_id,sessionId);
  assert.equal(adminRows.data[0].sha256,sha);
  const raceRecord=adminRows.data.find(r=>r.id===racingArtifact);
  assert(raceRecord && raceRecord.sha256===winningHash,
    "Concurrent staging metadata did not bind to the single stored winner");
  const academic=await jsonRequest("/rest/v1/session_artifacts?select=id",
    {headers:secretHeaders});
  assert.equal(academic.data.length,0,"Quarantine must not mint academic evidence");
  const studentTable=await fetch(api+"/rest/v1/evidence_upload_staging?select=id",{
    headers:studentHeaders(token)
  });
  assert(!studentTable.ok,"Authenticated client unexpectedly reads quarantined metadata");
  console.log("U04 local HTTP: Auth/RPC/Edge/Storage 201/200/409/403/422, conflicting concurrent write isolation, private-byte reconciliation, RLS; no academic evidence.");
}
await main();
