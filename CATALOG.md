# OriathHub plugin catalog

This repo hosts the OriathHub marketplace catalog and the tool that builds it.

- **`source.json`** — the hand-edited plugin list (the source of truth).
- **`DataProcessor/`** — a small .NET tool that reads `source.json`, queries the GitHub API for each
  repo's latest commit and releases, and emits the processed **`output.json`** the marketplace reads.
- **`.github/workflows/build-catalog.yml`** — runs the tool on every `source.json` change (and on a
  schedule) and commits `output.json` to the **`data`** branch.

```
source.json  ──(DataProcessor build)──►  output.json   (on the `data` branch)
   main branch        GitHub Action            └─ raw URL consumed by the marketplace
```

## source.json shape

```json
{
  "Plugins": [
    {
      "Name": "HealthBars",
      "OriginalAuthor": "danthespal",
      "Description": "Floating monster and player health bars overlay.",
      "EndorsedAuthor": "danthespal",
      "Repositories": [
        { "Author": "danthespal", "Name": "OriathHub-HealthBars", "Branch": null, "Location": null }
      ]
    }
  ]
}
```

- `Name` — plugin display name; also the folder the marketplace clones into. Keep it stable.
- `OriginalAuthor` / `Description` / `EndorsedAuthor` — catalog metadata. `EndorsedAuthor` selects the
  preferred fork when several exist.
- `Repositories[]` — one or more forks:
  - `Author` — display author of this fork.
  - `Name` — repository name.
  - `Branch` — non-default branch to track, or `null` for the repo's default branch.
  - `Location` — GitHub owner login if the repo lives under a different account/org than `Author`,
    else `null`. The clone URL is `https://github.com/<Location||Author>/<Name>.git`.

Each repository must be a buildable OriathHub plugin (a `net10.0-windows` csproj referencing
`OriathHub.Sdk` with the `CopyToHostPluginsDir` deploy target).

## Run the processor locally

```bash
# token avoids GitHub rate limits; a classic PAT with public_repo scope is enough
export gh_token=ghp_xxx
dotnet run --project DataProcessor -c Release -- build source.json > output.json
```

`build` reads the catalog from the given file (or stdin) and writes `output.json` to stdout; all
diagnostics go to stderr.

## One-time setup

1. Push this repo to GitHub with `source.json` on the default branch (`main`).
2. Create an empty **`data`** branch with a placeholder `output.json` (the workflow checks it out):

   ```bash
   git switch --orphan data
   echo '{ "PluginDescriptions": [], "Updated": "1970-01-01T00:00:00Z", "ModelVersion": "1" }' > output.json
   git add output.json && git commit -m "Seed data branch"
   git push -u origin data
   git switch main
   ```

3. The default `GITHUB_TOKEN` the workflow uses is provided automatically — no secret needed for
   public repos. Run the workflow once via **Actions → Build catalog output.json → Run workflow**.

4. Point the marketplace at the raw `output.json`:

   ```
   https://raw.githubusercontent.com/<owner>/<repo>/data/output.json
   ```
