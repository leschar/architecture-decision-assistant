using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Common;
using MediatR;

namespace ArchitectureAssistant.Application.Projects;

public sealed record GetProjectQuery(Guid ProjectId) : IRequest<Result<ProjectView>>;

public sealed class GetProjectHandler(IProjectRepository repository)
    : IRequestHandler<GetProjectQuery, Result<ProjectView>>
{
    public async Task<Result<ProjectView>> Handle(
        GetProjectQuery request,
        CancellationToken cancellationToken)
    {
        var result = await repository.LoadAsync(request.ProjectId, cancellationToken);
        return result.TryGetValue(out var project)
            ? Result<ProjectView>.Success(ProjectView.From(project))
            : Result<ProjectView>.Failure(result.Errors);
    }
}
