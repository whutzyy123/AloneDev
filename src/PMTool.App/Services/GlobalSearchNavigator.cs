using PMTool.Core.Models.Search;
using PMTool.App.ViewModels;

namespace PMTool.App.Services;

public sealed class GlobalSearchNavigator(
    ShellViewModel shellViewModel,
    ProjectListViewModel projectListViewModel,
    FeatureListViewModel featureListViewModel,
    TaskListViewModel taskListViewModel,
    DocumentListViewModel documentListViewModel,
    IdeaListViewModel ideaListViewModel) : IGlobalSearchNavigationService
{
    public async Task NavigateToHitAsync(GlobalSearchHit hit, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        var navKey = hit.Module switch
        {
            GlobalSearchModule.Project => "projects",
            GlobalSearchModule.Feature => "features",
            GlobalSearchModule.Task => "tasks",
            GlobalSearchModule.Document => "documents",
            GlobalSearchModule.Idea => "ideas",
            _ => "projects",
        };

        shellViewModel.NavigateToPrimaryModule(navKey);
        await Task.Yield();

        switch (hit.Module)
        {
            case GlobalSearchModule.Project:
                await projectListViewModel.JumpToEntityFromSearchAsync(hit.EntityId).ConfigureAwait(true);
                break;
            case GlobalSearchModule.Feature:
                if (hit.Jump.ProjectId is { Length: > 0 } fpid)
                {
                    await featureListViewModel.JumpToEntityFromSearchAsync(hit.EntityId, fpid).ConfigureAwait(true);
                }

                break;
            case GlobalSearchModule.Task:
                if (hit.Jump.ProjectId is { Length: > 0 } tpid)
                {
                    await taskListViewModel
                        .JumpToEntityFromSearchAsync(hit.EntityId, tpid, hit.Jump.FeatureId)
                        .ConfigureAwait(true);
                }

                break;
            case GlobalSearchModule.Document:
                await documentListViewModel.JumpToEntityFromSearchAsync(hit.EntityId).ConfigureAwait(true);
                break;
            case GlobalSearchModule.Idea:
                await ideaListViewModel.JumpToEntityFromSearchAsync(hit.EntityId).ConfigureAwait(true);
                break;
        }
    }
}
