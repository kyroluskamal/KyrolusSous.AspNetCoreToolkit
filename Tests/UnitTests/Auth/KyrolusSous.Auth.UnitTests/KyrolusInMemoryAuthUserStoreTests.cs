using KyrolusSous.Auth.Abstractions;
using KyrolusSous.Auth.Runtime;
using Shouldly;

namespace KyrolusSous.Auth.UnitTests;

public sealed class KyrolusInMemoryAuthUserStoreTests
{
    private readonly KyrolusInMemoryAuthUserStore _store = new();

    [Fact(DisplayName = "Add assigns an id when the record has none")]
    public void Add_assigns_an_id_when_the_record_has_none()
    {
        var user = _store.Add(new KyrolusAuthUser { UserName = "ada" });

        user.Id.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "Add keeps an id the caller supplied")]
    public void Add_keeps_an_id_the_caller_supplied()
    {
        var user = _store.Add(new KyrolusAuthUser { Id = "fixed", UserName = "ada" });

        user.Id.ShouldBe("fixed");
    }

    [Fact(DisplayName = "Lookups by name and email ignore case")]
    public async Task Lookups_by_name_and_email_ignore_case()
    {
        _store.Add(new KyrolusAuthUser { UserName = "Ada", Email = "Ada@Contoso.com" });

        (await _store.FindByUserNameAsync("ada")).ShouldNotBeNull();
        (await _store.FindByEmailAsync("ada@contoso.com")).ShouldNotBeNull();
    }

    [Fact(DisplayName = "A missing user comes back as null")]
    public async Task A_missing_user_comes_back_as_null()
    {
        (await _store.FindByIdAsync("nope")).ShouldBeNull();
        (await _store.FindByUserNameAsync("nope")).ShouldBeNull();
        (await _store.FindByEmailAsync("nope@contoso.com")).ShouldBeNull();
        (await _store.FindByExternalLoginAsync("Google", "nope")).ShouldBeNull();
    }

    [Fact(DisplayName = "External login keys do not collide across provider boundaries")]
    public async Task External_login_keys_do_not_collide_across_provider_boundaries()
    {
        var first = _store.Add(new KyrolusAuthUser { UserName = "first" });
        var second = _store.Add(new KyrolusAuthUser { UserName = "second" });

        // Concatenated without a separator these two would produce the same key, "Google123".
        await _store.AddExternalLoginAsync(first.Id, "Goog", "le123");
        await _store.AddExternalLoginAsync(second.Id, "Google", "123");

        (await _store.FindByExternalLoginAsync("Goog", "le123"))!.Id.ShouldBe(first.Id);
        (await _store.FindByExternalLoginAsync("Google", "123"))!.Id.ShouldBe(second.Id);
    }

    [Fact(DisplayName = "Recording and clearing failures round trips")]
    public async Task Recording_and_clearing_failures_round_trips()
    {
        var user = _store.Add(new KyrolusAuthUser { UserName = "ada" });
        var until = DateTimeOffset.UtcNow.AddMinutes(5);

        await _store.RecordFailedAttemptAsync(user.Id, 3, until);

        user.AccessFailedCount.ShouldBe(3);
        user.LockoutEnd.ShouldBe(until);

        await _store.ResetFailedAttemptsAsync(user.Id);

        user.AccessFailedCount.ShouldBe(0);
        user.LockoutEnd.ShouldBeNull();
    }

    [Fact(DisplayName = "Is Locked Out only counts a window that has not passed")]
    public void IsLockedOut_only_counts_a_window_that_has_not_passed()
    {
        var now = DateTimeOffset.UtcNow;
        var user = new KyrolusAuthUser { LockoutEnd = now.AddMinutes(5) };

        user.IsLockedOut(now).ShouldBeTrue();
        user.IsLockedOut(now.AddMinutes(6)).ShouldBeFalse();

        user.LockoutEnabled = false;
        user.IsLockedOut(now).ShouldBeFalse();
    }
}
