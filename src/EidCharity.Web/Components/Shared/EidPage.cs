using EidCharity.Domain;
using EidCharity.Infrastructure.Data;
using EidCharity.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace EidCharity.Web.Components.Shared;

public class Toasts
{
    public record Toast(Guid Id, string Text, string Kind);
    private readonly object _gate = new();
    public List<Toast> Items { get; } = [];
    public event Action? Changed;

    public void Show(string text, string kind = "success")
    {
        var t = new Toast(Guid.NewGuid(), text, kind);
        lock (_gate) Items.Add(t);
        Changed?.Invoke();
        _ = Task.Delay(kind == "error" ? 7000 : 3500).ContinueWith(_ => Dismiss(t.Id));
    }

    public void Dismiss(Guid id) { lock (_gate) Items.RemoveAll(t => t.Id == id); Changed?.Invoke(); }
    public List<Toast> Snapshot() { lock (_gate) return Items.ToList(); }
}

/// <summary>
/// Base for every screen: language-aware, permission helpers, and a guarded action runner that
/// prevents double submission, keeps the user's inputs on failure, and shows clear bilingual errors.
/// Each page owns its own DI scope (fresh DbContext), so data is never stale between screens and the
/// layout's queries never collide with the page's queries.
/// </summary>
public abstract class EidPage : OwningComponentBase
{
    [Inject] protected Ui Ui { get; set; } = default!;
    [Inject] protected CurrentUser Me { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected Toasts Toast { get; set; } = default!;
    [Inject] protected ILogger<EidPage> Log { get; set; } = default!;

    protected bool Busy { get; private set; }
    protected bool Loading { get; set; } = true;
    protected string? Error { get; set; }

    protected string T(string ar, string en) => Ui.T(ar, en);
    protected bool Can(string permission) => Me.Has(permission);
    protected bool CanAny(params string[] permissions) => permissions.Any(Me.Has);

    /// <summary>Resolves an application service from this page's own scope.</summary>
    protected TService S<TService>() where TService : notnull => ScopedServices.GetRequiredService<TService>();

    protected override void OnInitialized()
    {
        ScopedServices.GetRequiredService<CurrentUser>().Use(Me.User);
        Ui.Changed += OnUiChanged;
    }

    private void OnUiChanged() => InvokeAsync(StateHasChanged);

    protected override async Task OnParametersSetAsync()
    {
        Loading = true;
        Error = null;
        try { await LoadAsync(); }
        catch (DomainException ex) { Error = ex.For(Ui.IsAr); }
        catch (Exception ex) { Log.LogError(ex, "Load failed"); Error = T("تعذر تحميل البيانات. حاول مرة أخرى.", "Could not load data. Please try again."); }
        finally { Loading = false; }
    }

    protected virtual Task LoadAsync() => Task.CompletedTask;

    protected async Task Reload()
    {
        ClearTracking();
        try { await LoadAsync(); } catch (DomainException ex) { Error = ex.For(Ui.IsAr); }
        StateHasChanged();
    }

    private void ClearTracking() => ScopedServices.GetRequiredService<EidDbContext>().ChangeTracker.Clear();

    /// <summary>Runs an action once at a time. Returns true on success. Nothing half-done is ever saved later.</summary>
    protected async Task<bool> Run(Func<Task> action, string? okAr = null, string? okEn = null, bool reload = true)
    {
        if (Busy) return false;
        Busy = true;
        StateHasChanged();
        try
        {
            ClearTracking();
            await action();
            if (okAr is not null) Toast.Show(T(okAr, okEn ?? okAr));
            if (reload) { ClearTracking(); await LoadAsync(); }
            return true;
        }
        catch (DomainException ex) { ClearTracking(); Toast.Show(ex.For(Ui.IsAr), "error"); return false; }
        catch (DbUpdateConcurrencyException)
        {
            ClearTracking();
            Toast.Show(T("تم تعديل السجل من مستخدم آخر. أعد التحميل ثم حاول مجددًا.", "The record was changed by someone else. Reload and retry."), "error");
            return false;
        }
        catch (Exception ex)
        {
            ClearTracking();
            Log.LogError(ex, "Action failed");
            Toast.Show(T("حدث خطأ غير متوقع ولم يُنفذ الإجراء. مدخلاتك محفوظة.", "Unexpected error; nothing was applied. Your inputs are kept."), "error");
            return false;
        }
        finally { Busy = false; StateHasChanged(); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Ui.Changed -= OnUiChanged;
        base.Dispose(disposing);
    }
}
