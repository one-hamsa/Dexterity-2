-- Dexterity ObjectBinding: which member does a binding invoke by reflection?
--
-- ObjectBinding.Initialize() resolves {target, methodName} with Reflection.GetMethod, then
-- GetField, then GetProperty -- so the member kind is unknown from the asset and this view
-- deliberately does not filter on md.kind.
--
-- 71% of bindings in this project store a compiler-generated accessor name (get_isDay for
-- property isDay), because ObjectBinding records whatever GetMethod matched. Stripping the
-- get_/set_ prefix is therefore load-bearing, not a nicety: without it only 29% resolve.

DROP VIEW IF EXISTS v_dexterity_bindings;
CREATE VIEW v_dexterity_bindings AS
SELECT r.asset_path,
       r.owner_fileid,
       -- The owning component is what actually "uses" the member: a Dexterity source sitting
       -- on some GameObject. Its name is all the index holds (no transform parent chain), so
       -- the editor resolves the full hierarchy path from these two at display time.
       owner.go_name AS owner_go_name,
       owner_gm.path AS owner_script,
       r.member_name,
       td.full_name  AS target_type,
       md.id         AS member_id,
       md.name       AS member,
       md.kind       AS member_kind,
       td.file_path,
       md.line_number
  FROM asset_member_refs r
  LEFT JOIN asset_components owner ON owner.asset_path = r.asset_path AND owner.file_id = r.owner_fileid
  LEFT JOIN guid_map owner_gm ON owner_gm.guid = owner.script_guid
  JOIN asset_components ac ON ac.asset_path = r.asset_path AND ac.file_id = r.target_fileid
  JOIN guid_map gm         ON gm.guid = ac.script_guid
  JOIN type_declarations td ON td.file_path = gm.path
  JOIN member_declarations md ON md.type_id = td.id
   AND (md.name = r.member_name
        OR 'get_' || md.name = r.member_name
        OR 'set_' || md.name = r.member_name)
 WHERE r.rule_id = 'dexterity-binding'
   AND r.target_fileid IS NOT NULL AND r.target_fileid != 0;

-- Bindings whose target is unset in the asset itself (11% of them) -- the real target arrives
-- as a prefab-instance override, so the two halves live in different files. Listed separately
-- rather than joined in, so a caller sees what is unresolved instead of silently missing it.
DROP VIEW IF EXISTS v_dexterity_bindings_unresolved;
CREATE VIEW v_dexterity_bindings_unresolved AS
SELECT r.asset_path, r.owner_fileid, r.member_name
  FROM asset_member_refs r
 WHERE r.rule_id = 'dexterity-binding'
   AND (r.target_fileid IS NULL OR r.target_fileid = 0);

-- Deliberately NO v_binding_dexterity view. The DROP stays: these files are additive DDL, so
-- deleting a CREATE does not un-create anything — a DB that ran an earlier version keeps the
-- stale view (and would render Dexterity twice) until something drops it.
DROP VIEW IF EXISTS v_binding_dexterity;
--
-- That view is the zero-code UI contract: Codecierge unions every v_binding_* into
-- v_member_bindings and renders generic "member <-> asset" rows from it. Dexterity outgrew
-- that shape -- a binding's real user is a GameObject, not the containing asset, and one
-- member is regularly driven from several of them -- so it contributes UI in C# instead
-- (DexterityBindingSection + DexterityLineAnnotator, in Dexterity.Editor).
--
-- Defining both would render Dexterity twice: once as a generic foldout from the union and
-- once from its own section. A mapping picks one tier.
