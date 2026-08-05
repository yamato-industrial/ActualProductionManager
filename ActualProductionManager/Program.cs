using ActualProductionManager.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// DbContext ‚ğ DI ƒRƒ“ƒeƒi‚É“o˜^
builder.Services.AddDbContext<ActualProductionContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration["Database:ConnectionString"];
    var schema = configuration["Database:Schema"];

    options.UseNpgsql(connectionString, postgresqlOptions =>
    {
        postgresqlOptions.SetPostgresVersion(14, 2);
    });
});

var app = builder.Build();

// ŠÂ‹«•Êˆ—
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
