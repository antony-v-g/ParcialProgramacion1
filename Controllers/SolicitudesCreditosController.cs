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
        {
            ModelState.AddModelError("MontoMin", "El monto mínimo no puede ser negativo.");
        }

        if (filtros.MontoMax.HasValue && filtros.MontoMax.Value < 0)
        {
            ModelState.AddModelError("MontoMax", "El monto máximo no puede ser negativo.");
        }

        if (filtros.MontoMin.HasValue && filtros.MontoMax.HasValue &&
            filtros.MontoMin.Value > filtros.MontoMax.Value)
        {
            ModelState.AddModelError("MontoMax", "El monto mínimo no puede ser mayor que el monto máximo.");
        }

        if (filtros.FechaInicio.HasValue && filtros.FechaFin.HasValue &&
            filtros.FechaInicio.Value > filtros.FechaFin.Value)
        {
            ModelState.AddModelError("FechaFin", "La fecha de inicio no puede ser mayor que la fecha fin.");
        }

        if (ModelState.IsValid)
        {
            if (filtros.Estado.HasValue)
            {
                query = query.Where(s => s.Estado == filtros.Estado.Value);
            }

            if (filtros.MontoMin.HasValue)
            {
                query = query.Where(s => s.MontoSolicitado >= filtros.MontoMin.Value);
            }

            if (filtros.MontoMax.HasValue)
            {
                query = query.Where(s => s.MontoSolicitado <= filtros.MontoMax.Value);
            }

            if (filtros.FechaInicio.HasValue)
            {
                query = query.Where(s => s.FechaSolicitud.Date >= filtros.FechaInicio.Value.Date);
            }

            if (filtros.FechaFin.HasValue)
            {
                query = query.Where(s => s.FechaSolicitud.Date <= filtros.FechaFin.Value.Date);
            }
        }

        filtros.Solicitudes = await query
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();

        return View(filtros);
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
        {
            return NotFound();
        }

        return View(solicitud);
    }
}