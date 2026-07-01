using System.Text.Json;
using Octokit;
using OriathHub.Catalog.DataProcessor;

// Usage: DataProcessor build [source.json]
//   Reads the source catalog from the given file (or stdin), enriches each repository with its latest
//   commit and releases from the GitHub API, and writes the processed output.json to stdout.
//   Token is read from the gh_token or GITHUB_TOKEN environment variable.
//
// Only stdout receives the JSON; all diagnostics go to stderr, so `DataProcessor build src > out.json`
// produces a clean file.

if (args.Length == 0 || !string.Equals(args[0], "build", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Usage: DataProcessor build [source.json]");
    Console.Error.WriteLine("  Reads the source catalog from the file or stdin; writes processed output.json to stdout.");
    return 1;
}

string inputJson;
if (args.Length >= 2)
{
    inputJson = await File.ReadAllTextAsync(args[1]);
}
else
{
    using var stdin = Console.OpenStandardInput();
    using var reader = new StreamReader(stdin);
    inputJson = await reader.ReadToEndAsync();
}

var readOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
InputModel input;
try
{
    input = JsonSerializer.Deserialize<InputModel>(inputJson, readOptions)
            ?? throw new InvalidOperationException("source catalog deserialized to null.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to parse source catalog: {ex.Message}");
    return 1;
}

var token = Environment.GetEnvironmentVariable("gh_token")
            ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("Warning: no GitHub token (set gh_token or GITHUB_TOKEN). Unauthenticated API calls are heavily rate-limited.");
}

var github = new GitHubClient(new ProductHeaderValue("OriathHub-Catalog-DataProcessor"));
if (!string.IsNullOrWhiteSpace(token))
{
    github.Credentials = new Credentials(token);
}

var plugins = new List<ProcessedPlugin>();
foreach (var plugin in input.Plugins)
{
    var forks = new List<ProcessedFork>();
    foreach (var repository in plugin.Repositories)
    {
        var fork = await ProcessFork(github, repository);
        if (fork != null)
        {
            forks.Add(fork);
        }
    }

    plugins.Add(new ProcessedPlugin(plugin.Name, plugin.OriginalAuthor, forks, plugin.Description, plugin.EndorsedAuthor));
    Console.Error.WriteLine($"Processed {plugin.Name}: {forks.Count}/{plugin.Repositories.Count} fork(s).");
}

var output = new ProcessedCatalog(plugins, DateTime.UtcNow, "1");
var writeOptions = new JsonSerializerOptions { WriteIndented = true };
Console.Out.Write(JsonSerializer.Serialize(output, writeOptions));
return 0;

static async Task<ProcessedFork?> ProcessFork(GitHubClient github, RepositoryInfo repository)
{
    var owner = string.IsNullOrWhiteSpace(repository.Location) ? repository.Author : repository.Location;

    for (var attempt = 1; attempt <= 3; attempt++)
    {
        try
        {
            var repo = await github.Repository.Get(owner, repository.Name);

            var releases = await github.Repository.Release.GetAll(repo.Id, new ApiOptions { PageCount = 1, PageSize = 10 });
            var releaseInfos = new List<ReleaseInfo>();
            foreach (var r in releases.Where(r => r.PublishedAt != null))
            {
                releaseInfos.Add(new ReleaseInfo(
                    r.TagName ?? string.Empty,
                    string.IsNullOrWhiteSpace(r.Name) ? (r.TagName ?? string.Empty) : r.Name,
                    r.Assets.Select(a => a.Name).ToList(),
                    r.Body ?? string.Empty,
                    r.CreatedAt.UtcDateTime));
            }

            var branchName = string.IsNullOrWhiteSpace(repository.Branch) ? repo.DefaultBranch : repository.Branch;
            var branch = await github.Repository.Branch.Get(repo.Id, branchName);
            var commit = await github.Repository.Commit.Get(repo.Id, branch.Commit.Sha);
            var commitInfo = new CommitInfo(
                commit.Commit.Message,
                commit.Sha,
                commit.Author?.Login ?? commit.Commit.Author.Name,
                commit.Commit.Committer.Date.UtcDateTime);

            return new ProcessedFork(repository.Author, owner, repository.Name, commitInfo, releaseInfos);
        }
        catch (NotFoundException)
        {
            Console.Error.WriteLine($"  Repository not found: {owner}/{repository.Name} (skipped).");
            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"  Error processing {owner}/{repository.Name} (attempt {attempt}/3): {ex.Message}");
            if (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }

    return null;
}
