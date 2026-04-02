namespace TastileDesktop.Tests;

public sealed class CodeReviewFixesContractTests
{
    private static string ReadRepoFile(params string[] parts)
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        var path = Path.Combine([repoRoot, .. parts]);
        return File.ReadAllText(path);
    }

    [Fact]
    public void ApiModelsSource_EditableTileView_NestedObjectsAreNullable()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Models", "ApiModels.cs");

        Assert.Contains("[property: JsonPropertyName(\"temporal\")] CreateTileTemporalRequest? Temporal", source);
        Assert.Contains("[property: JsonPropertyName(\"objective\")] CreateTileObjectiveRequest? Objective", source);
        Assert.Contains("[property: JsonPropertyName(\"interruption\")] CreateTileInterruptionRequest? Interruption", source);
        Assert.Contains("[property: JsonPropertyName(\"automation\")] CreateTileAutomationRequest? Automation", source);
        Assert.Contains("[property: JsonPropertyName(\"annotation\")] CreateTileAnnotationRequest? Annotation", source);
    }

    [Fact]
    public void CoreApiClientSource_StreamLoop_DoesNotUseEndOfStreamGuard()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Services", "CoreApiClient.cs");

        Assert.DoesNotContain("while (!reader.EndOfStream", source);
    }

    [Fact]
    public void MainViewModelSource_NextQuickCandidates_DoesNotBlindlySkipFirstReadyTile()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "ViewModels", "MainViewModel.cs");

        Assert.DoesNotContain(".Skip(1).Take(5)", source);
    }

    [Fact]
    public void TileListItemMapperSource_FormatsNextStartLabel_InsteadOfRawTimestamp()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Services", "TileListItemMapper.cs");

        Assert.DoesNotContain("NextStartLabel = tv.ProjectedNextStartAt", source);
    }

    [Fact]
    public void CreateTileWindowSource_EditHydration_HandlesMissingNestedObjects()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Views", "CreateTileWindow.xaml.cs");

        Assert.Contains("var annotation = editTile.Annotation;", source);
        Assert.Contains("var objective = editTile.Objective;", source);
        Assert.Contains("var interruption = editTile.Interruption;", source);
        Assert.Contains("var temporal = editTile.Temporal;", source);
    }

    [Fact]
    public void CreateTileWindowSource_SaveButtonLabel_IsLocalized()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Views", "CreateTileWindow.xaml.cs");

        Assert.Contains("CreateButton.Content = _isJapanese ? \"保存\" : \"Save\";", source);
    }

    [Fact]
    public void TileHashResolverSource_Build_HandlesNullTilesSafely()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Services", "TileHashResolver.cs");

        Assert.Contains("if (current?.Tiles == null)", source);
    }
}
