// U04 admission for a bounded JSON evidence staging operation.
// This checks session ownership and bytes; it DOES NOT attest Native origin.
const uuid=/^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;
const digest=/^[0-9a-f]{64}$/;
export const MAX_EVIDENCE_BYTES=1048576;

export function evidenceObjectKey(tenantId,artifactId){
  if(!uuid.test(tenantId)||!uuid.test(artifactId))
    throw new Error("Canonical institution/artifact UUID required");
  return tenantId+"/"+artifactId;
}

export function checkUploadAdmission(args){
  if(!args || !uuid.test(args.tenantId) ||
    !uuid.test(args.artifactId) || !uuid.test(args.sessionId) ||
    !uuid.test(args.user?.id) || !args.user?.email_confirmed_at ||
    args.membership?.institution_id !== args.tenantId ||
    args.membership?.user_id !== args.user.id ||
    args.membership?.role !== "student" ||
    args.membership?.status !== "active" ||
    args.institution?.id !== args.tenantId ||
    args.institution?.status !== "active" ||
    args.session?.id !== args.sessionId ||
    args.session?.institution_id !== args.tenantId ||
    args.session?.user_id !== args.user.id ||
    args.session?.sync_state !== "pending" ||
    args.registrationAudit?.institution_id !== args.tenantId ||
    args.registrationAudit?.actor_id !== args.user.id ||
    args.registrationAudit?.target_id !== args.sessionId ||
    args.registrationAudit?.action !== "StagingLabSessionCreated" ||
    args.registrationAudit?.target_type !== "lab_sessions" ||
    args.registrationAudit?.details?.attested !== false ||
    args.registrationAudit?.details?.nativeRevision !== 0 ||
    !digest.test(args.sha256) ||
    args.contentType !== "application/json" ||
    !Number.isInteger(args.sizeBytes) ||
    args.sizeBytes < 1 || args.sizeBytes > MAX_EVIDENCE_BYTES) {
    throw new Error("Evidence admission denied");
  }
  return {artifactId:args.artifactId,objectKey:evidenceObjectKey(args.tenantId,args.artifactId)};
}

export async function verifyEvidenceBytes(bytes,expectedSha256,expectedLength){
  if(!(bytes instanceof Uint8Array) || bytes.length < 1 ||
    bytes.length > MAX_EVIDENCE_BYTES || bytes.length !== expectedLength ||
    typeof expectedSha256!=="string" || !digest.test(expectedSha256))
    throw new Error("Invalid evidence byte length or declared SHA-256");
  const hash=await crypto.subtle.digest("SHA-256",bytes);
  const actual=Array.from(new Uint8Array(hash))
    .map(x=>x.toString(16).padStart(2,"0")).join("");
  if(actual!==expectedSha256)throw new Error("Evidence byte digest mismatch");
  let decoded;
  try {decoded=JSON.parse(new TextDecoder("utf-8",{fatal:true}).decode(bytes));}
  catch {throw new Error("Native evidence JSON is invalid");}
  if(!decoded || Array.isArray(decoded) || typeof decoded!=="object")
    throw new Error("Evidence must be a JSON object");
  return actual;
}
