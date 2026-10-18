import { evidenceObjectKey, MAX_EVIDENCE_BYTES } from "./intent.mjs";

const digest=/^[0-9a-f]{64}$/;
const size=/^[0-9]{1,8}$/;
export function parseUploadHeaders(headers){
  if(!headers || headers.get("content-type")!=="application/json")
    throw new Error("U04 JSON evidence content type required");
  const tenantId=headers.get("x-institution-id");
  const sessionId=headers.get("x-session-id");
  const artifactId=headers.get("x-artifact-id");
  const sha256=headers.get("x-evidence-sha256");
  evidenceObjectKey(tenantId,artifactId);
  evidenceObjectKey(tenantId,sessionId);
  if(!digest.test(sha256??""))
    throw new Error("U04 valid lowercase SHA-256 header required");
  return {tenantId,sessionId,artifactId,sha256};
}

export async function readBoundedEvidence(request){
  const declared=request.headers.get("content-length");
  if(declared!==null && (!size.test(declared) ||
    Number(declared)<1 || Number(declared)>MAX_EVIDENCE_BYTES))
    throw new Error("Evidence Content-Length exceeds upload allowance");
  if(!request.body)throw new Error("Missing evidence request body");
  const reader=request.body.getReader();
  const chunks=[];
  let n=0;
  try{
    for(;;){
      const {done,value}=await reader.read();
      if(done)break;
      if(!(value instanceof Uint8Array))
        throw new Error("Invalid request body chunk");
      n+=value.length;
      if(n>MAX_EVIDENCE_BYTES){
        await reader.cancel();
        throw new Error("Evidence upload exceeds streaming limit");
      }
      chunks.push(value);
    }
  }finally{
    reader.releaseLock();
  }
  if(n<1 || (declared!==null && n!==Number(declared)))
    throw new Error("Evidence actual length does not match declared length");
  const out=new Uint8Array(n);
  let offset=0;
  for(const chunk of chunks){out.set(chunk,offset);offset+=chunk.length;}
  return out;
}
