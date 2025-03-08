using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BimManagement.Commands.ModelAudit
{
    public class GeometryValidator
    {
        private const double DistanceLimitInMeters = 1500; // Límite en metros

        public ValidationResult ValidateGeometryDistance()
        {
            try
            {
                // Convertir 1500 metros a unidades internas de Revit
                double distanceLimitInternal = UnitUtils.ConvertToInternalUnits(DistanceLimitInMeters, UnitTypeId.Meters);

                // Obtener el punto base del proyecto
                var projectBasePoint = new FilteredElementCollector(RevitTools.doc)
                    .OfCategory(BuiltInCategory.OST_ProjectBasePoint)
                    .OfClass(typeof(BasePoint))
                    .FirstOrDefault() as BasePoint;

                if (projectBasePoint == null)
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        Message = "No se encontró el punto base del proyecto en el modelo.",
                        IsRelevant = false
                    };
                }

                // Posición del punto base del proyecto
                XYZ basePointPosition = projectBasePoint.Position;

                // Recopilar elementos del modelo que contengan geometría
                var elements = new FilteredElementCollector(RevitTools.doc)
                    .WhereElementIsNotElementType()
                    .Where(e => e.get_Geometry(new Options()) != null)
                    .ToList();

                // Lista para almacenar IDs de elementos que excedan el límite permitido
                var invalidElementIds = new List<ElementId>();

                foreach (var element in elements)
                {
                    GeometryElement geometry = element.get_Geometry(new Options());
                    if (geometry == null) continue;

                    foreach (GeometryObject geomObj in geometry)
                    {
                        if (geomObj is GeometryInstance geomInstance)
                        {
                            foreach (GeometryObject instanceObj in geomInstance.GetInstanceGeometry())
                            {
                                if (instanceObj is Solid solid)
                                {
                                    if (IsSolidFarFromBasePoint(solid, basePointPosition, distanceLimitInternal))
                                    {
                                        invalidElementIds.Add(element.Id);
                                        break;
                                    }
                                }
                            }
                        }
                        else if (geomObj is Solid solid)
                        {
                            if (IsSolidFarFromBasePoint(solid, basePointPosition, distanceLimitInternal))
                            {
                                invalidElementIds.Add(element.Id);
                                break;
                            }
                        }
                    }
                }

                // Devolver resultado
                if (invalidElementIds.Any())
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        Message = $"Los siguientes ID están fuera del rango: {string.Join("\n", invalidElementIds)}",
                        IsRelevant = true
                    };
                }

                return new ValidationResult
                {
                    IsValid = true,
                    Message = string.Empty,
                    IsRelevant = true
                };
            }
            catch (Exception ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = $"Error interno: {ex.Message}",
                    IsRelevant = false
                };
            }
        }

        private bool IsSolidFarFromBasePoint(Solid solid, XYZ basePoint, double distanceLimit)
        {
            foreach (Edge edge in solid.Edges)
            {
                foreach (XYZ point in edge.Tessellate())
                {
                    if (basePoint.DistanceTo(point) > distanceLimit)
                    {
                        return true; // El punto está fuera del rango permitido
                    }
                }
            }
            return false;
        }
    }
}
