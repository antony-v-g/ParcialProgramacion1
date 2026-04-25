using ParcialProgramacion1.Models;

namespace ParcialProgramacion1.ViewModels;

public class SolicitudResumenViewModel
{
    public int Id { get; set; }
    public decimal MontoSolicitado { get; set; }
    public DateTime FechaSolicitud { get; set; }
    public EstadoSolicitud Estado { get; set; }
    public decimal IngresosMensuales { get; set; }
}