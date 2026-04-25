using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ParcialProgramacion1.Data;
using ParcialProgramacion1.Models;
using ParcialProgramacion1.ViewModels;

namespace ParcialProgramacion1.Controllers;

[Authorize]
public class SolicitudesCreditoController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<IdentityUser> _userManager;

    public SolicitudesCreditoController(
        ApplicationDbContext context,
        UserManager<IdentityUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(SolicitudFiltroViewModel filtros)
    {
        var usuarioId = _userManager.GetUserId(User);

        var query = _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .Where(s => s.Cliente != null && s.Cliente.UsuarioId == usuarioId)
            .AsQueryable();

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

        if (ModelState.IsValid)
        {
            if (filtros.Estado.HasValue)
                query = query.Where(s => s.Estado == filtros.Estado.Value);

            if (filtros.MontoMin.HasValue)
                query = query.Where(s => s.MontoSolicitado >= filtros.MontoMin.Value);

            if (filtros.MontoMax.HasValue)
                query = query.Where(s => s.MontoSolicitado <= filtros.MontoMax.Value);

            if (filtros.FechaInicio.HasValue)
                query = query.Where(s => s.FechaSolicitud.Date >= filtros.FechaInicio.Value.Date);

            if (filtros.FechaFin.HasValue)
                query = query.Where(s => s.FechaSolicitud.Date <= filtros.FechaFin.Value.Date);
        }

        filtros.Solicitudes = await query
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

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

        TempData["Exito"] = "Solicitud registrada correctamente en estado Pendiente.";

        return RedirectToAction(nameof(Create));
    }

    public async Task<IActionResult> Details(int id)
    {
        var usuarioId = _userManager.GetUserId(User);

        var solicitud = await _context.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s =>
                s.Id == id &&
                s.Cliente != null &&
                s.Cliente.UsuarioId == usuarioId);

        if (solicitud == null)
            return NotFound();

        return View(solicitud);
    }
}