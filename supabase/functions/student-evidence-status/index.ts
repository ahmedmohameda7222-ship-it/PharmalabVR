// PharmaLabVR English-only, authenticated READ-ONLY evidence metadata.
// No service-role credentials, Storage signed URLs or write capabilities.
import { createClient } from "npm:@supabase/supabase-js@2.57.4";
import { assertStudentEvidenceAccess, isCanonicalTenant } from "./policy.mjs";

const headers = {
  "Content-Type": "application/json; charset=utf-8",
  "Cache-Control": "no-store",
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET,OPTIONS",
  "Access-Control-Allow-Headers": "authorization,apikey,x-client-info",
};
const reply = (status: number, data: unknown) =>
  new Response(JSON.stringify(data), { status, headers });

Deno.serve(async (request: Request) => {
  if (request.method === "OPTIONS") return new Response(null, { status: 204, headers });
  if (request.method !== "GET") return reply(405, { error: "Method not supported" });
  const bearer = request.headers.get("Authorization") ?? "";
  if (!/^Bearer [A-Za-z0-9._-]+$/.test(bearer))
    return reply(401, { error: "Authenticated student required" });
  const tenantId = new URL(request.url).searchParams.get("institution_id");
  if (!isCanonicalTenant(tenantId))
    return reply(400, { error: "Institution ID must be a canonical UUID" });

  const endpoint = Deno.env.get("SUPABASE_URL");
  const publicKey = Deno.env.get("SUPABASE_PUBLISHABLE_KEY") ??
                    Deno.env.get("SUPABASE_ANON_KEY");
  if (!endpoint || !publicKey) return reply(503, { error: "Service unavailable" });
  const client = createClient(endpoint, publicKey, {
    global: { headers: { Authorization: bearer } },
    auth: { persistSession: false, autoRefreshToken: false, detectSessionInUrl: false },
  });
  try {
    const token = bearer.slice(7);
    const { data: identity, error: authError } = await client.auth.getUser(token);
    if (authError || !identity.user?.email_confirmed_at)
      return reply(401, { error: "Verified Auth session required" });
    const actor = identity.user;
    const [member, school] = await Promise.all([
      client.from("institution_memberships")
        .select("institution_id,user_id,role,status")
        .eq("institution_id", tenantId).eq("user_id", actor.id).maybeSingle(),
      client.from("institutions").select("id,status")
        .eq("id", tenantId).maybeSingle(),
    ]);
    if (member.error || school.error)
      return reply(503, { error: "Membership lookup unavailable" });
    try {
      assertStudentEvidenceAccess(tenantId, actor, member.data, school.data);
    } catch (_) {
      return reply(403, { error: "Student membership required" });
    }

    // Row-level security remains authoritative for every returned row.
    const [sessions, artifacts] = await Promise.all([
      client.from("lab_sessions")
        .select("id,local_session_id,native_branch_id,current_revision,science_package_sha256,sync_state")
        .eq("institution_id", tenantId).eq("user_id", actor.id)
        .order("id", { ascending: true }).limit(50),
      client.from("session_artifacts")
        .select("id,lab_session_id,sha256,size_bytes,content_type,recorded_at")
        .eq("institution_id", tenantId).eq("user_id", actor.id)
        .order("recorded_at", { ascending: false }).limit(50),
    ]);
    if (sessions.error || artifacts.error)
      return reply(503, { error: "Evidence query unavailable" });
    return reply(200, {
      institutionId: tenantId,
      sessions: sessions.data ?? [],
      artifacts: artifacts.data ?? [],
      hasMore: null,
      syncAuthority: "NativeOfflineFirst",
      note: "Metadata only; the scientific solver remains local",
    });
  } catch (_) {
    return reply(503, { error: "Evidence service unavailable" });
  }
});
