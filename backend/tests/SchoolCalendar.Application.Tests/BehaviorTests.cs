using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SchoolCalendar.Application.Common.Authorization;
using SchoolCalendar.Application.Common.Behaviors;
using SchoolCalendar.Application.Common.Caching;
using SchoolCalendar.Application.Common.Exceptions;
using SchoolCalendar.Application.Common.Interfaces;
using SchoolCalendar.Infrastructure.Caching;

namespace SchoolCalendar.Application.Tests;

public class AuthorizationBehaviorTests
{
    private sealed record ProtectedRequest(string SchoolId) : IRequest<string>, IAuthorizedRequest
    {
        public IEnumerable<PermissionRequirement> GetPermissionRequirements() =>
            [new(ResourceTypes.School, SchoolId, Permissions.View)];
    }

    private sealed record UnmarkedRequest : IRequest<string>;

    private readonly ICurrentUser _user = Substitute.For<ICurrentUser>();
    private readonly IPermissionService _permify = Substitute.For<IPermissionService>();

    private AuthorizationBehavior<T, string> Behavior<T>() where T : notnull =>
        new(_user, _permify, NullLogger<AuthorizationBehavior<T, string>>.Instance);

    [Fact]
    public async Task Allowed_request_reaches_handler()
    {
        _user.UserId.Returns("alice");
        _permify.CheckAsync(Arg.Any<PermissionRequirement>(), "alice", Arg.Any<CancellationToken>()).Returns(true);

        var result = await Behavior<ProtectedRequest>().Handle(new ProtectedRequest("s1"), _ => Task.FromResult("ok"), default);

        Assert.Equal("ok", result);
        await _permify.Received(1).CheckAsync(new(ResourceTypes.School, "s1", Permissions.View), "alice", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Denied_request_throws_forbidden_and_skips_handler()
    {
        _user.UserId.Returns("bob");
        _permify.CheckAsync(Arg.Any<PermissionRequirement>(), "bob", Arg.Any<CancellationToken>()).Returns(false);
        var handlerCalled = false;

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            Behavior<ProtectedRequest>().Handle(new ProtectedRequest("s1"), _ => { handlerCalled = true; return Task.FromResult("ok"); }, default));
        Assert.False(handlerCalled);
    }

    [Fact]
    public async Task Anonymous_caller_is_rejected()
    {
        _user.UserId.Returns((string?)null);
        await Assert.ThrowsAsync<UnauthenticatedException>(() =>
            Behavior<ProtectedRequest>().Handle(new ProtectedRequest("s1"), _ => Task.FromResult("ok"), default));
    }

    [Fact]
    public async Task Request_without_authorization_contract_is_rejected()
    {
        _user.UserId.Returns("alice");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Behavior<UnmarkedRequest>().Handle(new UnmarkedRequest(), _ => Task.FromResult("ok"), default));
    }

    [Fact]
    public void Every_request_in_the_application_declares_an_authorization_contract()
    {
        var requestTypes = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.GetInterfaces().Any(i =>
                i == typeof(IRequest) || (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))));

        var missing = requestTypes
            .Where(t => !typeof(IAuthorizedRequest).IsAssignableFrom(t) && !typeof(IAuthorizedByLookup).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(missing);
    }
}

public class CachingTests
{
    private sealed record CachedQuery(Guid Id) : IRequest<string>, ICacheableQuery
    {
        public string CacheKey => $"q:{Id}";
        public IReadOnlyCollection<string> CacheTags => [Common.Caching.CacheTags.School(Id)];
    }

    private static DistributedCacheService NewCache() =>
        new(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), NullLogger<DistributedCacheService>.Instance);

    [Fact]
    public async Task Second_call_is_served_from_cache_until_tag_is_invalidated()
    {
        var cache = NewCache();
        var behavior = new CachingBehavior<CachedQuery, string>(cache, NullLogger<CachingBehavior<CachedQuery, string>>.Instance);
        var query = new CachedQuery(Guid.NewGuid());
        var calls = 0;
        RequestHandlerDelegate<string> handler = _ => Task.FromResult($"v{++calls}");

        Assert.Equal("v1", await behavior.Handle(query, handler, default));
        Assert.Equal("v1", await behavior.Handle(query, handler, default));

        await cache.InvalidateTagsAsync([CacheTags.School(query.Id)]);
        Assert.Equal("v2", await behavior.Handle(query, handler, default));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Invalidating_one_tag_leaves_other_entries_cached()
    {
        var cache = NewCache();
        await cache.SetAsync("a", "A", ["t1"]);
        await cache.SetAsync("b", "B", ["t2"]);

        await cache.InvalidateTagsAsync(["t1"]);

        Assert.Null(await cache.GetAsync<string>("a", ["t1"]));
        Assert.Equal("B", await cache.GetAsync<string>("b", ["t2"]));
    }
}
