using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using static Autodesk.Revit.DB.SpecTypeId;

namespace BimManagement.Controller
{
    public class AddSharedParameters
    {
        public static ICollection<Element> elements { get; set; }
        public static List<string> ExistingParameters { get; set; }


        public static void AddAndFillParameters()
        {
            //Crear parametros segun categorias y lista de parametros para Grupo concreto
            //IList<SharedParameter> sharedParameters = SharedParameters
            //    .GetAllSharedParameters().OrderBy(p => p.Name).ToList();
            IList<SharedParameter> sharedParameters = SharedParameters.GetAllSharedParameters();

            // Parametros que existen en el modelo
            ExistingParameters = GetExistParameters();

            using (Transaction transaction = new Transaction(RevitTools.doc, "Agregar Parametros Compartidos"))
            {
                transaction.Start();

                foreach (var parameter in sharedParameters)
                {
                    //Location of the shared parameters
                    string oriFile = RevitTools.app.SharedParametersFilename;

                    //My Documents
                    var newpath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                    //Create txt inside my documents
                    Extract("BimManagement", newpath, "Resources", "CASA-PAR-RIBA_5.txt");

                    //Create variable with the location on SP.txt inside my documents
                    string tempFile = newpath + @"\CASA-PAR-RIBA_5.txt";

                    AddParameter(parameter.Guid, GroupTypeId.Data, parameter.Name, tempFile);

                    //Place original SP file 
                    RevitTools.app.SharedParametersFilename = oriFile;

                    //Delete SP temporary file
                    System.IO.File.Delete(tempFile);
                }

                transaction.Commit();
            }

            //LLenar Parametros
            FillSharedParameter.FillParameter(sharedParameters);
        }

        /// <summary>
        /// Copy and embedded resource to a directory(txt file in this case)
        /// </summary>
        /// <param name="nameSpace"></param>
        /// <param name="outDirectory"></param>
        /// <param name="internalFilePath"></param>
        /// <param name="resourceName"></param>
        private static void Extract(string nameSpace, string outDirectory,
            string internalFilePath, string resourceName)
        {
            //Assembly assembly = Assembly.GetCallingAssembly();
            Assembly assembly = Assembly.GetExecutingAssembly();

            string name = nameSpace + "." + (internalFilePath == "" ? "" : internalFilePath + ".") + resourceName;

            using (Stream s = assembly.GetManifestResourceStream(name))
            using (BinaryReader r = new BinaryReader(s))
            using (FileStream fs = new FileStream(outDirectory + "\\" + resourceName, FileMode.OpenOrCreate))
            using (BinaryWriter w = new BinaryWriter(fs))
                w.Write(r.ReadBytes((int)s.Length));
        }

        private static void AddParameter(Guid guiParameter, ForgeTypeId group, string name, string tempFile)
        {
            //Crea el set de categoria de refuerzos
            Categories categories = RevitTools.doc.Settings.Categories;
            Category catParts = categories.get_Item(BuiltInCategory.OST_Parts);
            Category catBeam = categories.get_Item(BuiltInCategory.OST_StructuralFraming);
            Category catColumn = categories.get_Item(BuiltInCategory.OST_StructuralColumns);
            Category catFoundation = categories.get_Item(BuiltInCategory.OST_StructuralFoundation);
            Category catFloor = categories.get_Item(BuiltInCategory.OST_Floors);
            Category catWall = categories.get_Item(BuiltInCategory.OST_Walls);
            Category catGeneric = categories.get_Item(BuiltInCategory.OST_GenericModel);
            Category catRebar = categories.get_Item(BuiltInCategory.OST_Rebar);
            CategorySet catSet = new CategorySet();

            catSet.Insert(catParts);
            catSet.Insert(catBeam);
            catSet.Insert(catColumn);
            catSet.Insert(catFoundation);
            catSet.Insert(catFloor);
            catSet.Insert(catWall);

            if (!name.Contains("03_03_01") &&
                !name.Contains("03_03_02"))
            {
                catSet.Insert(catRebar);
            }

            if (!name.Contains("03_03_01") &&
                !name.Contains("03_03_03"))
            {
                catSet.Insert(catGeneric);
            }

            //Verificar si existe y solo modificar categorias
            if (ExistingParameters.Contains(name))
            {
                ModifyExistingParameters(name, group, catSet);
                return;
            }

            //Change the location of the shared parameters for the SP location
            RevitTools.app.SharedParametersFilename = tempFile;
            DefinitionFile defFile = RevitTools.app.OpenSharedParameterFile();

            var v = (from DefinitionGroup dg in defFile.Groups
                     from ExternalDefinition d in dg.Definitions
                     where d.GUID == guiParameter
                     select d);

            //Valido si existe el parametro
            if (v == null || v.Count() < 1) { return; }

            ExternalDefinition def = v.First();
            //Debug.WriteLine(def.Name);

            //Autodesk.Revit.DB.Binding binding = RevitTools.app.Create.NewTypeBinding(cats);
            Autodesk.Revit.DB.Binding binding = RevitTools.app.Create.NewInstanceBinding(catSet);

            BindingMap map = (new UIApplication(RevitTools.app)).ActiveUIDocument.Document.ParameterBindings;
            map.Insert(def, binding, group);
        }

        static List<string> GetExistParameters()
        {
            List<string> parameterNames = new List<string>();

            elements = RevitTools.GetElements();

            foreach (Element element in elements)
            {
                IList<Parameter> parameters = element.GetOrderedParameters();

                foreach (Parameter parameter in parameters)
                {
                    if (!parameter.IsShared) continue;

                    string paramName = parameter.Definition.Name;
                    if (!parameterNames.Contains(paramName))
                    {
                        parameterNames.Add(paramName);
                    }
                }
            }

            return parameterNames;
        }

        static void ModifyExistingParameters(string name, ForgeTypeId group, CategorySet categorySet)
        {
            //si existe agregarle las categorias necesarias
            // Obtiene el parámetro existente
            DefinitionBindingMapIterator it = RevitTools.doc.ParameterBindings.ForwardIterator();
            while (it.MoveNext())
            {
                Definition definition = it.Key as Definition;
                if (definition != null && definition.Name == name)
                {
                    // Elimina el enlace existente
                    RevitTools.doc.ParameterBindings.Remove(definition);
                    Binding binding = RevitTools.app.Create.NewInstanceBinding(categorySet);
                    RevitTools.doc.ParameterBindings.Insert(definition, binding, group);
                    break;
                }
            }
        }
    }
}
