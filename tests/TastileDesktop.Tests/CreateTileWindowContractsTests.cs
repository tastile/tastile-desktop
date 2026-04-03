using TastileDesktop.Services;

namespace TastileDesktop.Tests;

public sealed class CreateTileWindowContractsTests
{
    private static string ReadRepoFile(params string[] parts)
    {
        var baseDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", ".."));
        var path = Path.Combine([repoRoot, .. parts]);
        return File.ReadAllText(path);
    }

    [Fact]
    public void ResolveWindowText_EditMode_UsesEditHeadingAndSaveButton()
    {
        var contract = CreateTileWindowContractResolver.ResolveWindowText(
            isEditMode: true,
            isJapanese: false);

        Assert.Equal("Edit Tile", contract.WindowTitle);
        Assert.Equal("Edit Tile", contract.HeadingText);
        Assert.Equal("Save", contract.PrimaryButtonText);
    }

    [Fact]
    public void ResolveWindowText_CreateMode_UsesCreateHeadingAndCreateButton()
    {
        var contract = CreateTileWindowContractResolver.ResolveWindowText(
            isEditMode: false,
            isJapanese: true);

        Assert.Equal("Create Tile", contract.WindowTitle);
        Assert.Equal("Create Tile", contract.HeadingText);
        Assert.Equal("作成", contract.PrimaryButtonText);
    }

    [Fact]
    public void ShouldClearSuggestedTitleOnFirstFocus_OnlyWhenAutoSuggested()
    {
        Assert.True(CreateTileWindowContractResolver.ShouldClearSuggestedTitleOnFirstFocus(
            currentTitle: "作業 25分",
            suggestedTitle: "作業 25分",
            titleEdited: false,
            alreadyClearedOnFocus: false));

        Assert.False(CreateTileWindowContractResolver.ShouldClearSuggestedTitleOnFirstFocus(
            currentTitle: "custom title",
            suggestedTitle: "作業 25分",
            titleEdited: true,
            alreadyClearedOnFocus: false));

        Assert.False(CreateTileWindowContractResolver.ShouldClearSuggestedTitleOnFirstFocus(
            currentTitle: "作業 25分",
            suggestedTitle: "作業 25分",
            titleEdited: false,
            alreadyClearedOnFocus: true));
    }

    [Fact]
    public void ShouldShowBaseTimingPanel_HidesForRecurring()
    {
        Assert.True(CreateTileWindowContractResolver.ShouldShowBaseTimingPanel("finish_once"));
        Assert.False(CreateTileWindowContractResolver.ShouldShowBaseTimingPanel("recurring"));
    }

    [Fact]
    public void ResolveDurationUpdate_PreservesManualInput_WhenManualDurationExists()
    {
        var contract = CreateTileWindowContractResolver.ResolveDurationUpdate(
            autoDurationMinutes: 90,
            durationManuallyEdited: true);

        Assert.Null(contract.Hours);
        Assert.Null(contract.Minutes);
    }

    [Fact]
    public void ResolveDurationUpdate_UsesAutoDuration_WhenManualDurationIsNotEdited()
    {
        var contract = CreateTileWindowContractResolver.ResolveDurationUpdate(
            autoDurationMinutes: 95,
            durationManuallyEdited: false);

        Assert.Equal(1, contract.Hours);
        Assert.Equal(35, contract.Minutes);
    }

    [Fact]
    public void CreateTileWindowSource_CreateConflictPrompt_UsesConflictResolutionToastPath()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Views", "CreateTileWindow.xaml.cs");

        Assert.Contains("TryCreateWithConflictResolutionAsync(request)", source);
        Assert.Contains("if (result?.Prompt?.Kind != \"create_conflict\")", source);
        Assert.Contains("ShowConflictResolutionToastAsync(result.Prompt)", source);
    }
}
