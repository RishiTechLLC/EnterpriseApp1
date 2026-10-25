using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using SchoolCalendar.Application.Common.Authorization;
using SchoolCalendar.Application.Common.Caching;
using SchoolCalendar.Application.Common.Exceptions;
using SchoolCalendar.Application.Common.Interfaces;
using ValidationException = SchoolCalendar.Application.Common.Exceptions.ValidationException;

namespace SchoolCalendar.Application.Common.Behaviors;

// Pipeline order (outermost first): Validation -> Authorization -> Caching -> Handler.

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();
        if (failures.Count != 0) throw new ValidationException(failures);

        return await next();
    }
}

/// <summary>
/// Checks every permission a request declares against Permify before the handler runs.
/// A request that declares neither <see cref="IAuthorizedRequest"/> nor <see cref="IAuthorizedByLookup"/>
/// is rejected, so no API call can reach a handler without an access decision.
/// </summary>
public sealed class AuthorizationBehavior<TRequest, TResponse>(
    ICurrentUser currentUser,
    IPermissionService permissionService,
    ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthenticatedException();

        switch (request)
        {
            case IAuthorizedRequest authorized:
                var requirements = authorized.GetPermissionRequirements().ToList();
                var results = await Task.WhenAll(requirements.Select(r => permissionService.CheckAsync(r, userId, cancellationToken)));
                for (var i = 0; i < requirements.Count; i++)
                {
                    if (results[i]) continue;
                    var r = requirements[i];
                    logger.LogWarning("Permify denied {User} '{Permission}' on {EntityType}:{EntityId} for {Request}",
                        userId, r.Permission, r.EntityType, r.EntityId, typeof(TRequest).Name);
                    throw new ForbiddenAccessException();
                }
                break;

            case IAuthorizedByLookup:
                break;

            default:
                throw new InvalidOperationException(
                    $"{typeof(TRequest).Name} must implement {nameof(IAuthorizedRequest)} or {nameof(IAuthorizedByLookup)}.");
        }

        return await next();
    }
}

public sealed class CachingBehavior<TRequest, TResponse>(ICacheService cache, ILogger<CachingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromMinutes(10);

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICacheableQuery query) return await next();

        var cached = await cache.GetAsync<TResponse>(query.CacheKey, query.CacheTags, cancellationToken);
        if (cached is not null)
        {
            logger.LogDebug("Cache hit {CacheKey}", query.CacheKey);
            return cached;
        }

        var response = await next();
        await cache.SetAsync(query.CacheKey, response, query.CacheTags, query.CacheDuration ?? DefaultDuration, cancellationToken);
        return response;
    }
}
