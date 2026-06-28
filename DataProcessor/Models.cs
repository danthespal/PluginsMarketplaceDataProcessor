namespace OriathHub.Catalog.DataProcessor
{
    // ---- Input: the hand-edited source.json -------------------------------------------------

    public record InputModel(List<PluginInfo> Plugins);

    public record PluginInfo(
        string Name,
        string OriginalAuthor,
        List<RepositoryInfo> Repositories,
        string Description,
        string? EndorsedAuthor);

    /// <summary>
    ///     A repository (or fork) for a plugin. <see cref="Location"/> is the GitHub owner login when the
    ///     repo lives under a different account/org than the display <see cref="Author"/>; <see cref="Branch"/>
    ///     tracks a non-default branch.
    /// </summary>
    public record RepositoryInfo(
        string Author,
        string Name,
        string? Branch,
        string? Location);

    // ---- Output: the processed output.json the marketplace consumes -------------------------
    // Property names MUST match the marketplace's ProcessedCatalog/ProcessedPlugin/ProcessedFork/
    // CommitInfo/ReleaseInfo parser. ModelVersion is "1".

    public record ProcessedCatalog(
        List<ProcessedPlugin> PluginDescriptions,
        DateTime Updated,
        string ModelVersion);

    public record ProcessedPlugin(
        string Name,
        string OriginalAuthor,
        List<ProcessedFork> Forks,
        string Description,
        string? EndorsedAuthor);

    public record ProcessedFork(
        string Author,
        string Location,
        string Name,
        CommitInfo? LatestCommit,
        List<ReleaseInfo> Releases);

    public record CommitInfo(
        string Message,
        string Hash,
        string Author,
        DateTime Date);

    public record ReleaseInfo(
        string Id,
        string Title,
        List<string> FilesAttached,
        string Description,
        DateTime Date,
        // Hex SHA-256 of the release's downloadable .zip asset, pinned so the marketplace can verify
        // the artifact it downloads before installing. Empty when the release has no .zip asset.
        string Sha256);
}
