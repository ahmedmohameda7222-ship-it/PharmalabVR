// U04 quarantine intake only. This endpoint never creates session_artifacts,
// signs a Native receipt, approves a grade or acknowledges cloud synchronization.
import { createClient } from "npm:@supabase/supabase-js@2.57.4";
import { checkUploadAdmission,verifyEvidenceBytes } from "./intent.mjs";
import { parseUploadHeaders,readBoundedEvidence } from "./protocol.mjs";

const cors={
  "Access-Control-Allow-Origin":"*",
  "Access-Control-Allow-Methods":"POST,OPTIONS",
  "Access-Control-Allow-Headers":"authorization,apikey,x-client-info,content-type,x-institution-id,x-session-id,x-artifact-id,x-evidence-sha256",
  "Cache-Control":"no-store",
  "Content-Type":"application/json; charset=utf-8",
};
const response=(status:number,data:unknown)=>
  new Response(JSON.stringify(data),{status,headers:cors});

function serverSecret():string|null{
  const secretKeys=Deno.env.get("SUPABASE_SECRET_KEYS");
  if(secretKeys){
    try {
      const data=JSON.parse(secretKeys);
      if(typeof data?.default==="string" && data.default.length>0)
        return data.default;
    }catch {return null;}
  }
  return Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? null;
}
Deno.serve(async(request:Request)=>{
  if(request.method==="OPTIONS")return new Response(null,{status:204,headers:cors});
  if(request.method!=="POST")return response(405,{error:"Method not supported"});
  const authorization=request.headers.get("authorization") ?? "";
  if(!/^Bearer [A-Za-z0-9._-]+$/.test(authorization))
    return response(401,{error:"Verified student access token required"});
  let metadata;
  try{metadata=parseUploadHeaders(request.headers);}
  catch{return response(400,{error:"Canonical evidence headers required"});}
  const url=Deno.env.get("SUPABASE_URL");
  const publicKey=Deno.env.get("SUPABASE_PUBLISHABLE_KEY") ??
                  Deno.env.get("SUPABASE_ANON_KEY");
  const secretKey=serverSecret();
  if(!url || !publicKey || !secretKey)
    return response(503,{error:"Staging service unavailable"});
  try{
    const studentClient=createClient(url,publicKey,{
      global:{headers:{Authorization:authorization}},
      auth:{persistSession:false,autoRefreshToken:false,detectSessionInUrl:false},
    });
    const jwt=authorization.slice(7);
    const {data:identity,error:identityError}=await studentClient.auth.getUser(jwt);
    if(identityError || !identity.user?.id || !identity.user.email_confirmed_at)
      return response(401,{error:"Current verified Auth session required"});
    const actor=identity.user;
    // All three reads run with the real user's JWT and existing RLS rules.
    const [member,school,nativeSession]=await Promise.all([
      studentClient.from("institution_memberships")
        .select("institution_id,user_id,role,status")
        .eq("institution_id",metadata.tenantId).eq("user_id",actor.id).maybeSingle(),
      studentClient.from("institutions")
        .select("id,status").eq("id",metadata.tenantId).maybeSingle(),
      studentClient.from("lab_sessions")
        .select("id,institution_id,user_id,sync_state")
        .eq("id",metadata.sessionId).eq("institution_id",metadata.tenantId)
        .eq("user_id",actor.id).maybeSingle(),
    ]);
    if(member.error || school.error || nativeSession.error)
      return response(503,{error:"Session authorization lookup unavailable"});
    let bytes:Uint8Array;
    try{bytes=await readBoundedEvidence(request);}
    catch{return response(413,{error:"Invalid or oversized evidence payload"});}
    let admission;
    try{
      admission=checkUploadAdmission({
        ...metadata,user:actor,
        membership:member.data,institution:school.data,session:nativeSession.data,
        sizeBytes:bytes.byteLength,contentType:"application/json",
      });
    }catch{return response(403,{error:"Active student and owned pending Native session required"});}
    try{await verifyEvidenceBytes(bytes,metadata.sha256,bytes.byteLength);}
    catch{return response(422,{error:"Evidence bytes or SHA-256 did not validate"});}
    // Service credential never leaves this function. No upsert, no academic
    // evidence bucket access or authoritative Native acknowledgment.
    const admin=createClient(url,secretKey,{
      auth:{persistSession:false,autoRefreshToken:false,detectSessionInUrl:false},
    });
    const bucket="plv-university-evidence-staging";
    const uploaded=await admin.storage.from(bucket).upload(admission.objectKey,bytes,{
      contentType:"application/json",cacheControl:"0",upsert:false,
    });
    if(uploaded.error){
      const code=String((uploaded.error as {statusCode?:string|number}).statusCode??"");
      return response(code==="409"?409:503,{
        error:code==="409"?"Evidence artifact already staged":"Private quarantine upload failed",
      });
    }
    const saved=await admin.from("evidence_upload_staging").insert({
      id:metadata.artifactId,institution_id:metadata.tenantId,
      lab_session_id:metadata.sessionId,student_id:actor.id,
      object_key:admission.objectKey,size_bytes:bytes.byteLength,
      sha256:metadata.sha256,content_type:"application/json",
    });
    if(saved.error){
      // Best-effort cleanup for the artifact just uploaded by this request.
      // Even if cleanup fails, orphaned staging objects are not academic proof.
      await admin.storage.from(bucket).remove([admission.objectKey]);
      return response(503,{error:"Quarantine metadata was not committed"});
    }
    return response(201,{
      state:"staged_unverified",institutionId:metadata.tenantId,
      artifactId:metadata.artifactId,sha256:metadata.sha256,
      note:"Quarantined bytes only; not Native-attested, synced or graded",
    });
  }catch{
    return response(503,{error:"Evidence staging service unavailable"});
  }
});
