// Pure access-policy contract shared by the deployed Edge Function and Node CI.
// Never trust JWT user_metadata or a caller-supplied role.
export function isCanonicalTenant(value) {
  return typeof value === "string" &&
    /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/.test(value);
}

export function assertStudentEvidenceAccess(tenantId,user,membership,institution) {
  if (!isCanonicalTenant(tenantId) || !user?.id || !user?.email_confirmed_at ||
      membership?.institution_id !== tenantId ||
      membership?.user_id !== user.id ||
      membership?.role !== "student" ||
      membership?.status !== "active" ||
      institution?.id !== tenantId ||
      institution?.status !== "active") {
    throw new Error("Student evidence access denied");
  }
  return tenantId;
}
