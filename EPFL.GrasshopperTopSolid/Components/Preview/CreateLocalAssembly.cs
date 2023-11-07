﻿using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using TopSolid.Cad.Design.DB;
using TopSolid.Cad.Design.DB.Documents;
using TopSolid.Cad.Design.DB.Local.Operations;
using TopSolid.Kernel.DB.D3.Shapes;
using TopSolid.Kernel.DB.Entities;
using TopSolid.Kernel.DB.Layers;
using TopSolid.Kernel.G.D3;
using TopSolid.Kernel.G.D3.Shapes;
using TopSolid.Kernel.G.D3.Shapes.Creations;
using TopSolid.Kernel.GR.Attributes;
using TopSolid.Kernel.SX;
using TopSolid.Kernel.SX.Drawing;
using TopSolid.Kernel.TX.Documents;
using TopSolid.Kernel.TX.Items;
using TopSolid.Kernel.TX.Pdm;
using TopSolid.Kernel.TX.Undo;
using TopSolid.Kernel.TX.Units;
using TK = TopSolid.Kernel;

namespace EPFL.GrasshopperTopSolid.Components.Preview
{
    public class CreateLocalAssembly : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CreateLocalAssembly class.
        /// </summary>
        public CreateLocalAssembly()
          : base("CreateLocalAssembly", "Nickname",
              "Bakes Rhino Geometry into Local Parts and Assemblies",
              "TopSolid", "Preview")
        {
        }
        bool run = false;
        AssemblyDocument assemblyDocument = TopSolid.Kernel.UI.Application.CurrentDocument as AssemblyDocument;
        EntityList parts;

        protected override void BeforeSolveInstance()
        {
            //volatileData = Params.Input[0].VolatileData;
            //needsLocalAssemblies = volatileData.PathCount > 1;

            try
            {
                UndoSequence.Start("Create Local Part", false);
            }

            catch
            {
                UndoSequence.UndoCurrent();
                UndoSequence.Start("Create Local Part", false);
            }
            base.BeforeSolveInstance();
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Geometry", "G", "Rhino Geometry to bake", GH_ParamAccess.item);
            pManager.AddGenericParameter("Assembly Document", "A", "target TopSolid Assembly to bake-in, if none provided will get current assembly", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddTextParameter("Name", "Name", "Name for Local Part Document", GH_ParamAccess.item);
            pManager[2].Optional = true;
            pManager.AddGenericParameter("TopSolid Attributes", "attributes", "TopSolid's attributes for the created entities", GH_ParamAccess.item);
            //pManager[3].Optional = true;
            pManager.AddBooleanParameter("Bake?", "b?", "Set true to bake", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_ObjectWrapper wrapper = new GH_ObjectWrapper();//needed for TopSolid types

            if (!DA.GetData("Bake?", ref run) || !run) return;

            if (DA.GetData("Assembly Document", ref wrapper))
            {
                assemblyDocument = GetAssemblyDocument(wrapper);
            }

            if (assemblyDocument == null)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Could not find valid Assembly");

            //The baking process starts on boolean true
            if (run == true)
            {
                parts = new EntityList();
                GH_String name = new GH_String();
                IGH_GeometricGoo rhinoGeometry = null;
                EntityList entities = new EntityList();
                Brep brep = null;
                GH_ObjectWrapper attributesWrapper = null;

                if (!DA.GetData("Geometry", ref rhinoGeometry)) return;
                DA.GetData("Name", ref name);

                LocalPartsCreation localPartCreation = new LocalPartsCreation(assemblyDocument, 0);

                ShapeEntity shapeEntity = new ShapeEntity(assemblyDocument, 0);
                //GH_Convert.ToBrep(rhinoGeometry, ref brep, GH_Conversion.Both);
                //Shape topSolidShape = brep.ToHost();
                DA.GetData("TopSolid Attributes", ref attributesWrapper);

                var topSolidAttributes = attributesWrapper.Value as Tuple<Transparency, Color, string>;
                //SetTopSolidEntity(topSolidAttributes, shapeEntity, topSolidShape);

                BlockMaker blockMaker = new BlockMaker(TK.SX.Version.Current);
                blockMaker.Frame = Frame.OXYZ;
                blockMaker.XLength = 0.01;
                blockMaker.YLength = 0.01;
                blockMaker.ZLength = 0.01;

                Shape shape = blockMaker.Make(shapeEntity, null);

                // Replace shape geometry.


                shapeEntity.Geometry = shape;


                entities.Add(shapeEntity);
                PartEntity partEntity = CreateLocalPart(assemblyDocument, entities, name.Value);

                TK.SX.Collections.Generic.List<PartEntity> CreatedPartEntities = new TK.SX.Collections.Generic.List<PartEntity>
                {
                    partEntity
                };

                localPartCreation.SetChildParts(CreatedPartEntities);
                localPartCreation.IsDeletable = true;
                localPartCreation.Name = "Creation Part : " + name.Value;

                parts.Add(partEntity);

                localPartCreation.Create();
                assemblyDocument.Update(true, true);



                //// Update primitive.
                AssemblyDefinitionCreation assemblyDefinitionCreation = new AssemblyDefinitionCreation(assemblyDocument, 0);
                assemblyDefinitionCreation.IsModifiable = true;
                assemblyDefinitionCreation.IsDeletable = true;
                assemblyDefinitionCreation.Name = "super assembly";
                assemblyDefinitionCreation.Create();

                assemblyDefinitionCreation.SetOriginals(parts);
                assemblyDocument.Update(true, true);
                //parts sont les pieces locales(ou pas locales), ou des assemblages locaux




            }
        }

        protected override void AfterSolveInstance()
        {
            if (UndoSequence.Current != null)
                UndoSequence.End();

            else
            {
                UndoSequence.UndoCurrent();
            }
            base.AfterSolveInstance();
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override System.Guid ComponentGuid
        {
            get { return new System.Guid("69A7C434-84CE-48A8-8D02-A3767B6035EC"); }
        }

        private PartEntity CreateLocalPart(AssemblyDocument inAssemblyDocument, EntityList inEntities, string inName)
        {
            PartEntity localPart = new PartEntity(inAssemblyDocument, 0);
            localPart.SetLocalConstituents(inEntities);

            localPart.MakeDefaultParameters(TK.SX.Version.Current, true);
            if (!string.IsNullOrEmpty(inName))
                localPart.NameParameterValue = inAssemblyDocument.MakeLocalizableString(inName);

            foreach (Entity entity in inEntities)
            {
                localPart.AddEntityToLocalRepresentation(entity, TopSolid.Cad.Design.DB.Documents.ElementName.DetailedRepresentation);
                localPart.AddEntityToLocalRepresentation(entity, TopSolid.Cad.Design.DB.Documents.ElementName.DesignRepresentation);
                localPart.AddEntityToLocalRepresentation(entity, TopSolid.Cad.Design.DB.Documents.ElementName.SimplifiedRepresentation);
            }
            return localPart;
        }

        private void SetTopSolidEntity(Tuple<Transparency, Color, string> topSolidAttributes, Entity entity, Shape topSolidShape)
        {
            Color topSolidColor = Color.Empty;
            Transparency topSolidtransparency = Transparency.Empty;
            string topSolidLayerName = "";
            Layer topSolidLayer = new Layer(-1);
            LayerEntity layerEntity = new LayerEntity(assemblyDocument, 0, topSolidLayer);

            if (topSolidAttributes != null)
            {
                topSolidColor = topSolidAttributes.Item2;
                topSolidtransparency = topSolidAttributes.Item1;
                topSolidLayerName = topSolidAttributes.Item3;
            }


            var layfoldEnt = LayersFolderEntity.GetOrCreateFolder(assemblyDocument);
            layerEntity = layfoldEnt.SearchLayer(topSolidLayerName);

            if (layerEntity == null)
            {
                layerEntity = new LayerEntity(assemblyDocument, 0, topSolidLayer);
                layerEntity.Name = topSolidLayerName;
            }


            entity.ExplicitColor = topSolidColor;
            entity.ExplicitLayer = topSolidLayer;
            entity.ExplicitTransparency = topSolidtransparency;
            //entity.SetGeometry(topSolidShape, true, true);

            //TODO
            entity.Geometry = topSolidShape;
        }

        private AssemblyDocument GetAssemblyDocument(GH_ObjectWrapper wrapper)
        {
            IDocument res = null;
            if (wrapper.Value is string || wrapper.Value is GH_String)
            {
                res = DocumentStore.Documents.Where(x => x.Name.ToString() == wrapper.Value.ToString()).FirstOrDefault();
                assemblyDocument = res as AssemblyDocument;
            }
            else if (wrapper.Value is IDocumentItem)
                assemblyDocument = (wrapper.Value as IDocumentItem).OpenLastValidMinorRevisionDocument() as AssemblyDocument;
            else if (wrapper.Value is IDocument)
                assemblyDocument = wrapper.Value as AssemblyDocument;

            if (assemblyDocument == null)
                assemblyDocument = TopSolid.Kernel.UI.Application.CurrentDocument as AssemblyDocument;

            return assemblyDocument;
        }
    }
}