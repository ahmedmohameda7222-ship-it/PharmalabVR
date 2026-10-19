// U04 retransmission is a quarantine retry, NEVER trusted scientific receipt.
// A duplicate Storage object alone is not proof of an accepted prior request.
import { evidenceObjectKey, verifyEvidenceBytes } from "./intent.mjs";

export function isDuplicateStagingUpload(error) {
  if(!error || !["400","409"].includes(String(error.statusCode??error.status)))
    return false;
  // Supabase Storage may include both a human-readable error string and a
  // stable machine code. Match the code first; messages are not API enums.
  const name=String(error.code??error.error??"");
  return ["Duplicate","ResourceAlreadyExists","KeyAlreadyExists"].includes(name);
}

export async function verifyExistingStaging(args) {
  const {row,tenantId,artifactId,sessionId,studentId,sha256,bytes}=args??{};
  const key=evidenceObjectKey(tenantId,artifactId);
  evidenceObjectKey(tenantId,sessionId);
  evidenceObjectKey(tenantId,studentId);
  if(!row || row.id!==artifactId ||
      row.institution_id!==tenantId || row.lab_session_id!==sessionId ||
      row.student_id!==studentId || row.object_key!==key ||
      row.content_type!=="application/json" || row.sha256!==sha256 ||
      !Number.isInteger(row.size_bytes))
    throw new Error("Staging retry scope or digest differs");
  await verifyEvidenceBytes(bytes,sha256,row.size_bytes);
  return {artifactId,sha256,alreadyStaged:true};
}
