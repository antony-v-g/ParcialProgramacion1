using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ParcialProgramacion1.Data;
using ParcialProgramacion1.Models;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=creditos.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
    options.InstanceName = "ParcialCreditos_";
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddControllersWithViews();

var app = builder.Build();

await SeedDataAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

async Task SeedDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();

    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    await context.Database.MigrateAsync();

    const string rolAnalista = "Analista";

    if (!await roleManager.RoleExistsAsync(rolAnalista))
    {
        await roleManager.CreateAsync(new IdentityRole(rolAnalista));
    }

    var email = configuration["SeedUser:Email"] ?? "analista@demo.com";
    var password = configuration["SeedUser:Password"] ?? "Analista123!";

    var usuario = await userManager.FindByEmailAsync(email);

    if (usuario == null)
    {
        usuario = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        await userManager.CreateAsync(usuario, password);
    }

    if (!await userManager.IsInRoleAsync(usuario, rolAnalista))
    {
        await userManager.AddToRoleAsync(usuario, rolAnalista);
    }

    if (!context.Clientes.Any())
    {
        var cliente1 = new Cliente
        {
            UsuarioId = usuario.Id,
            IngresosMensuales = 2500,
            Activo = true
        };

        var cliente2 = new Cliente
        {
            UsuarioId = usuario.Id,
            IngresosMensuales = 4000,
            Activo = true
        };

        context.Clientes.AddRange(cliente1, cliente2);
        await context.SaveChangesAsync();

        var solicitud1 = new SolicitudCredito
        {
            ClienteId = cliente1.Id,
            MontoSolicitado = 8000,
            FechaSolicitud = DateTime.Now,
            Estado = EstadoSolicitud.Pendiente
        };

        var solicitud2 = new SolicitudCredito
        {
            ClienteId = cliente2.Id,
            MontoSolicitado = 10000,
            FechaSolicitud = DateTime.Now.AddDays(-5),
            Estado = EstadoSolicitud.Aprobado
        };

        context.SolicitudesCredito.AddRange(solicitud1, solicitud2);
        await context.SaveChangesAsync();
    }
}