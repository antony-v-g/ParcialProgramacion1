using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using ParcialProgramacion1.Data;
using ParcialProgramacion1.Models;
using ParcialProgramacion1.ViewModels;

namespace ParcialProgramacion1.Controllers;

[Authorize]
public class SolicitudesCreditoController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IDistributedCache _cache;

    public SolicitudesCreditoController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager,
        IDistributedCache cache)
    {
        _context = context;
        _userManager = userManager;
        _cache = cache;
    }

    public async Task<IActionResult> Index(SolicitudFiltroViewModel filtros)
    {
        var usuarioId = _userManager.GetUserId(User);

        if (usuarioId == null)
            return Challenge();

        if (filtros.MontoMin.HasValue && filtros.MontoMin.Value < 0)
            ModelState.AddModelError("MontoMin", "El monto mínimo no puede ser negativo.");

        if (filtros.MontoMax.HasValue && filtros.MontoMax.Value < 0)
            ModelState.AddModelError("MontoMax", "El monto máximo no puede ser negativo.");

        if (filtros.MontoMin.HasValue && filtros.MontoMax.HasValue &&
            filtros.MontoMin.Value > filtros.MontoMax.Value)
            ModelState.AddModelError("MontoMax", "El monto mínimo no puede ser mayor que el monto máximo.");

        if (filtros.FechaInicio.HasValue && filtros.FechaFin.HasValue &&
            filtros.FechaInicio.Value > filtros.FechaFin.Value)
            ModelState.AddModelError("FechaFin", "La fecha de inicio no puede ser mayor que la fecha fin.");

        var cacheKey = ObtenerCacheKey(usuarioId);

        List<SolicitudResumenViewModel>? solicitudes;

        var cacheJson = await _cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cacheJson))
        {
            solicitudes = JsonSerializer.Deserialize<List<SolicitudResumenViewModel>>(cacheJson);
        }
        else
        {
            solicitudes = await _context.SolicitudesCredito
                .Include(s => s.Cliente)
                .Where(s => s.Cliente != null && s.Cliente.UsuarioId == usuarioId)
                .OrderByDescending(s => s.FechaSolicitud)
                .Select(s => new SolicitudResumenViewModel
                {
                    Id = s.Id,
                    MontoSolicitado = s.MontoSolicitado,
                    FechaSolicitud = s.FechaSolicitud,
                    Estado = s.Estado,
                    IngresosMensuales = s.Cliente!.IngresosMensuales
                })
                .ToListAsync();

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(solicitudes),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
                });
        }

        solicitudes ??= new List<SolicitudResumenViewModel>();

        if (ModelState.IsValid)
        {
            if (filtros.Estado.HasValue)
                solicitudes = solicitudes.Where(s => s.Estado == filtros.Estado.Value).ToList();

            if (filtros.MontoMin.HasValue)
                solicitudes = solicitudes.Where(s => s.MontoSolicitado >= filtros.MontoMin.Value).ToList();

            if (filtros.MontoMax.HasValue)
                solicitudes = solicitudes.Where(s => s.MontoSolicitado <= filtros.MontoMax.Value).ToList();

            if (filtros.FechaInicio.HasValue)
                solicitudes = solicitudes.Where(s => s.FechaSolicitud.Date >= filtros.FechaInicio.Value.Date).ToList();

            if (filtros.FechaFin.HasValue)
                solicitudes = solicitudes.Where(s => s.FechaSolicitud.Date <= filtros.FechaFin.Value.Date).ToList();
        }

        filtros.Solicitudes = solicitudes;

        return View(filtros);
    }

    public IActionResult Create()
    {
        return View(new RegistroSolicitudViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RegistroSolicitudViewModel model)
    {
        var usuarioId = _userManager.GetUserId(User);

        if (usuarioId == null)
            return Challenge();

        var cliente = await _context.Clientes
            .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId);

        if (cliente == null)
        {
            ModelState.AddModelError("", "No existe un cliente asociado al usuario autenticado.");
            return View(model);
        }

        if (!cliente.Activo)
        {
            ModelState.AddModelError("", "El cliente no está activo.");
            return View(model);
        }

        var tienePendiente = await _context.SolicitudesCredito
            .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

        if (tienePendiente)
        {
            ModelState.AddModelError("", "No puede registrar una nueva solicitud porque ya tiene una solicitud pendiente.");
            return View(model);
        }

        if (model.MontoSolicitado > cliente.IngresosMensuales * 10)
        {
            ModelState.AddModelError("MontoSolicitado", "El monto solicitado no puede superar 10 veces los ingresos mensuales.");
            return View(model);
        }

        if (!ModelState.IsValid)
            return View(model);

        var solicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = model.MontoSolicitado,
            FechaSolicitud = DateTime.Now,
            Estado = EstadoSolicitud.Pendiente
        };

        _context.SolicitudesCredito.Add(solicitud);
        await _context.SaveChangesAsync();

        await InvalidarCacheUsuario(usuarioId);

        TempData["Exito"] = "Solicitud registrada correctamente en estado Pendiente.";

        return RedirectToAction(nameof(Create));
    }

    public async Task<IActionResult> Details(int id)
    {
        var usuarioId = _userManager.GetUserId(User);

        if (usuarioId == null)
            return Challenge();

        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s =>
                s.Id == id &&
                s.Cliente != null &&
                s.Cliente.UsuarioId == usuarioId);

        if (solicitud == null)
            return NotFound();

        HttpContext.Session.SetInt32("UltimaSolicitudId", solicitud.Id);
        HttpContext.Session.SetString("UltimaSolicitudMonto", solicitud.MontoSolicitado.ToString("N2"));

        return View(solicitud);
    }

    private string ObtenerCacheKey(string usuarioId)
    {
        return $"solicitudes_usuario_{usuarioId}";
    }

    private async Task InvalidarCacheUsuario(string usuarioId)
    {
        await _cache.RemoveAsync(ObtenerCacheKey(usuarioId));
    }
}