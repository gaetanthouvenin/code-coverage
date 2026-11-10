-- Read-only: one SELECT, no temporary object. The totals of every finalized commit, per flag, as one JSON
-- array on one line. The project is the row whose flag is empty. Compare-TotalsWithCodecov.ps1 filters the
-- repository and the commits: the query takes no parameter, so that it runs unchanged in psql and in a test.
SELECT COALESCE(json_agg(row_to_json(t)), '[]'::json)
FROM (
  SELECT o."Forge"            AS "forge",
         r."Owner"            AS "owner",
         r."Namespace"        AS "namespace",
         r."Name"             AS "name",
         c."Sha"              AS "sha",
         c."State"            AS "state",
         f."Flag"             AS "flag",
         f."Lines"            AS "lines",
         f."Hits"             AS "hits",
         f."Misses"           AS "misses",
         f."Partials"         AS "partials",
         f."Coverage"         AS "coverage",
         f."IsCarriedForward" AS "isCarriedForward"
  FROM "CommitFlagTotals" f
  JOIN "Commits" c ON c."Id" = f."CommitId"
  JOIN "Repositories" r ON r."Id" = c."RepositoryId"
  JOIN "Organizations" o ON o."Id" = r."OrganizationId"
  ORDER BY o."Forge", r."Owner", r."Namespace", r."Name", c."Sha", f."Flag"
) t;
