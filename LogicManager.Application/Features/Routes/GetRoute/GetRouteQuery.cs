using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogicManager.Application.Features.Routes.GetRoute;

public class GetRouteQuery : IRequest<RouteDto>
{
    public int Id { get; set; } // Birincil anahtar
    public string? RouteName { get; set; }
    public string? Description { get; set; }
}
