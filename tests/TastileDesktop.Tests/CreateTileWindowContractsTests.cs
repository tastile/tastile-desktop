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
        // NF04-D04: the legacy daemon create_conflict prompt flow was
        // removed — v1 commands go through the offline outbox and
        // conflicts surface in the pending-operations list instead of a
        // create-time toast. This test now pins the outbox contract.
        var source = ReadRepoFile("src", "TastileDesktop", "Views", "CreateTileWindow.xaml.cs");

        Assert.Contains("OfflineCommandRuntime.Shared.Commands", source);
        Assert.Contains("commands.CreateTileAsync(", source);
        Assert.Contains("commands.UpdateTileAsync(", source);
        Assert.Contains("Commands.ArchiveTileAsync(", source);
        Assert.DoesNotContain("TryCreateWithConflictResolutionAsync", source);
    }

    [Fact]
    public void CreateTileWindowSource_EditSave_DoesNotTriggerLocalTick()
    {
        var source = ReadRepoFile("src", "TastileDesktop", "Views", "CreateTileWindow.xaml.cs");

        // Server-side recalculation handles post-edit state. The desktop must
        // not poke a 1-second tick on the local daemon any more.
        Assert.DoesNotContain("await _api.TriggerTickAsync();", source);
        Assert.DoesNotContain("/commands/tick", source);
    }
}
