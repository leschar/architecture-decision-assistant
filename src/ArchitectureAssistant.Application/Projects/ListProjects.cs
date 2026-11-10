using ArchitectureAssistant.Application.Abstractions.Persistence;
using ArchitectureAssistant.Domain.Common;
using MediatR;

namespace ArchitectureAssistant.Application.Projects;

public sealed record ListProjectsQuery : IRequest<Result<IReadOnlyList<ProjectListItem>>>;

public sealed class ListProjectsHandler(IProjectRepository repository)
    : IRequestHandler<ListProjectsQuery, Result<IReadOnlyList<ProjectListItem>>>
{
    public async Task<Result<IReadOnlyList<ProjectListItem>>> Handle(
        ListProjectsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await repository.ListAsync(cancellationToken);
        if (!result.TryGetValue(out var projects))
        {
            return Result<IReadOnlyList<ProjectListItem>>.Failure(result.Errors);
        }

        return Result<IReadOnlyList<ProjectListItem>>.Success(
            projects.Select(ProjectListItemMapper.From).ToArray());
    }
}
