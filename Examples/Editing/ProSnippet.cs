/*

   Copyright 2018 Esri

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

   See the License for the specific language governing permissions and
   limitations under the License.

*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Editing.Attributes;
using ArcGIS.Desktop.Editing.Events;
using ArcGIS.Desktop.Editing.Templates;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace EditingSDKExamples
{
  class ProSnippet : MapTool
  {

    Geometry _geometry;

    public ProSnippet()
    {
      IsSketchTool = true;
      SketchType = SketchGeometryType.Rectangle;
      SketchOutputMode = SketchOutputMode.Map;
    }

    protected override Task<bool> OnSketchCompleteAsync(Geometry geometry)
    {
      _geometry = geometry;
      return base.OnSketchCompleteAsync(geometry);
    }

    #region ProSnippet Group: Edit Operation Methods
    #endregion

    public void EditOperations() {

      var featureLayer = MapView.Active.Map.GetLayersAsFlattenedList()[0] as FeatureLayer;
      var polygon = new PolygonBuilder().ToGeometry();
      var clipPoly = new PolygonBuilder().ToGeometry();
      var cutLine = new PolylineBuilder().ToGeometry();
      var modifyLine = cutLine;
      var oid = 1;
      var layer = featureLayer;

      #region Edit Operation Create Features

      var createFeatures = new EditOperation();
      createFeatures.Name = "Create Features";
      //Create a feature with a polygon
      createFeatures.Create(featureLayer, polygon);

      //with a callback
      createFeatures.Create(featureLayer, polygon, (object_id) => {
          //TODO - use the oid of the created feature
          //in your callback
      });

      //Do a create features and set attributes
      var attributes = new Dictionary<string, object>();
      attributes.Add("SHAPE", polygon);
      attributes.Add("NAME", "Corner Market");
      attributes.Add("SIZE", 1200.5);
      attributes.Add("DESCRIPTION", "Corner Market");

      createFeatures.Create(featureLayer, attributes);

      //Create features using the current template
      //Must be within a MapTool
      createFeatures.Create(this.CurrentTemplate, polygon);

      //Execute to execute the operation
      //Must be called within QueuedTask.Run
      createFeatures.Execute();

      //or use async flavor
      //await createFeatures.ExecuteAsync();

      #endregion

      #region Create a feature using the current template
      var myTemplate = ArcGIS.Desktop.Editing.Templates.EditingTemplate.Current;
      var myGeometry = _geometry;

      //Create edit operation and execute
      var op = new ArcGIS.Desktop.Editing.EditOperation();
      op.Name = "Create my feature";
      op.Create(myTemplate, myGeometry);
      op.Execute();
      #endregion

      #region Create feature from a modified inspector

      var insp = new ArcGIS.Desktop.Editing.Attributes.Inspector();
      insp.Load(layer, 86);

      ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
      {
        // modify attributes if necessary
        // insp["Field1"] = newValue;

        //Create new feature from an existing inspector (copying the feature)
        var createOp = new ArcGIS.Desktop.Editing.EditOperation();
        createOp.Name = "Create from insp";
        createOp.Create(insp.MapMember, insp.ToDictionary(a => a.FieldName, a => a.CurrentValue));
        createOp.Execute();
      });
      #endregion

      var csvData = new List<CSVData>();

      #region Create features from a CSV file
      //Run on MCT
      ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
      {
        //Create the edit operation
        var createOperation = new ArcGIS.Desktop.Editing.EditOperation();
        createOperation.Name = "Generate points";
        createOperation.SelectNewFeatures = false;

        // determine the shape field name - it may not be 'Shape' 
        string shapeField = layer.GetFeatureClass().GetDefinition().GetShapeField();

        //Loop through csv data
        foreach (var item in csvData)
        {

          //Create the point geometry
          ArcGIS.Core.Geometry.MapPoint newMapPoint = ArcGIS.Core.Geometry.MapPointBuilder.CreateMapPoint(item.X, item.Y);

          // include the attributes via a dictionary
          var atts = new Dictionary<string, object>();
          atts.Add("StopOrder", item.StopOrder);
          atts.Add("FacilityID", item.FacilityID);
          atts.Add(shapeField, newMapPoint);

          // queue feature creation
          createOperation.Create(layer, atts);
        }

        // execute the edit (feature creation) operation
        return createOperation.Execute();
      });
      #endregion

      #region Edit Operation Clip Features

      var clipFeatures = new EditOperation();
      clipFeatures.Name = "Clip Features";
      clipFeatures.Clip(featureLayer, oid, clipPoly, ClipMode.PreserveArea);
      //Execute to execute the operation
      //Must be called within QueuedTask.Run
      clipFeatures.Execute();

      //or use async flavor
      //await clipFeatures.ExecuteAsync();

      #endregion

      #region Edit Operation Cut Features

      var select = MapView.Active.SelectFeatures(clipPoly);

      var cutFeatures = new EditOperation();
      cutFeatures.Name = "Cut Features";
      cutFeatures.Split(featureLayer, oid, cutLine);

      //Cut all the selected features in the active view
      //Select using a polygon (for example)
      var kvps = MapView.Active.SelectFeatures(polygon).Select(
            k => new KeyValuePair<MapMember, List<long>>(k.Key as MapMember, k.Value));
      cutFeatures.Split(kvps, cutLine);

      //Execute to execute the operation
      //Must be called within QueuedTask.Run
      cutFeatures.Execute();

      //or use async flavor
      //await cutFeatures.ExecuteAsync();

      #endregion

      #region Edit Operation Delete Features

      var deleteFeatures = new EditOperation();
      deleteFeatures.Name = "Delete Features";
      var table = MapView.Active.Map.StandaloneTables[0];
      //Delete a row in a standalone table
      deleteFeatures.Delete(table, oid);

      //Delete all the selected features in the active view
      //Select using a polygon (for example)
      var selection = MapView.Active.SelectFeatures(polygon).Select(
            k => new KeyValuePair<MapMember, List<long>>(k.Key as MapMember, k.Value));

      deleteFeatures.Delete(selection);

      //Execute to execute the operation
      //Must be called within QueuedTask.Run
      deleteFeatures.Execute();

      //or use async flavor
      //await deleteFeatures.ExecuteAsync();

      #endregion
      
      #region Edit Operation Duplicate Features

      var duplicateFeatures = new EditOperation();
      duplicateFeatures.Name = "Duplicate Features";

      //Duplicate with an X and Y offset of 500 map units
      duplicateFeatures.Duplicate(featureLayer, oid, 500.0, 500.0, 0.0);

      //Execute to execute the operation
      //Must be called within QueuedTask.Run
      duplicateFeatures.Execute();

      //or use async flavor
      //await duplicateFeatures.ExecuteAsync();

      #endregion

      #region Edit Operation Explode Features

      var explodeFeatures = new EditOperation();
      explodeFeatures.Name = "Explode Features";

      //Take a multipart and convert it into one feature per part
      //Provide a list of ids to convert multiple
      explodeFeatures.Explode(featureLayer, new List<long>() {oid}, true);

      //Execute to execute the operation
      //Must be called within QueuedTask.Run
      explodeFeatures.Execute();

      //or use async flavor
      //await explodeFeatures.ExecuteAsync();

      #endregion

      var destinationLayer = featureLayer;

      #region Edit Operation Merge Features

      var mergeFeatures = new EditOperation();
      mergeFeatures.Name = "Merge Features";

      //Merge three features into a new feature using defaults
      //defined in the current template
      mergeFeatures.Merge(this.CurrentTemplate as EditingFeatureTemplate, featureLayer, new List<long>() { 10, 96, 12 });

      //Merge three features into a new feature in the destination layer
      mergeFeatures.Merge(destinationLayer, featureLayer, new List<long>() { 10, 96, 12 });

      //Use an inspector to set the new attributes of the merged feature
      var inspector = new Inspector();
      inspector.Load(featureLayer, oid);//base attributes on an existing feature
      //change attributes for the new feature
      inspector["NAME"] = "New name";
      inspector["DESCRIPTION"] = "New description";

      //Merge features into a new feature in the same layer using the
      //defaults set in the inspector
      mergeFeatures.Merge(featureLayer, new List<long>() {10, 96, 12}, inspector);
            
      //Execute to execute the operation
      //Must be called within QueuedTask.Run
      mergeFeatures.Execute();

      //or use async flavor
      //await mergeFeatures.ExecuteAsync();

      #endregion

      #region Edit Operation Modify single feature

      var modifyFeature = new EditOperation();
      modifyFeature.Name = "Modify a feature";

      //use an inspector
      var modifyInspector = new Inspector();
      modifyInspector.Load(featureLayer, oid);//base attributes on an existing feature

      //change attributes for the new feature
      modifyInspector["SHAPE"] = polygon;//Update the geometry
      modifyInspector["NAME"] = "Updated name";//Update attribute(s)

      modifyFeature.Modify(modifyInspector);

      //update geometry and attributes using overload
      var featureAttributes = new Dictionary<string, object>();
      featureAttributes["NAME"] = "Updated name";//Update attribute(s)
      modifyFeature.Modify(featureLayer, oid, polygon, featureAttributes);

      //Execute to execute the operation
      //Must be called within QueuedTask.Run
      modifyFeature.Execute();

      //or use async flavor
      //await modifyFeatures.ExecuteAsync();

      #endregion

      #region Edit Operation Modify multiple features

      //Search by attribute
      var queryFilter = new QueryFilter();
      queryFilter.WhereClause = "OBJECTID < 1000000";
      //Create list of oids to update
      var oidSet = new List<long>();
      using (var rc = featureLayer.Search(queryFilter))
      {
        while (rc.MoveNext())
          oidSet.Add(rc.Current.GetObjectID());
      }

      //create and execute the edit operation
      var modifyFeatures = new EditOperation();
      modifyFeatures.Name = "Modify features";
      modifyFeatures.ShowProgressor = true;

      var muultipleFeaturesInsp = new Inspector();
      muultipleFeaturesInsp.Load(featureLayer, oidSet);
      muultipleFeaturesInsp["MOMC"] = 24;
      modifyFeatures.Modify(muultipleFeaturesInsp);
      modifyFeatures.ExecuteAsync();
      #endregion

      #region Search for layer features and update a field
      ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
      {
        //find layer
        var disLayer = ArcGIS.Desktop.Mapping.MapView.Active.Map.FindLayers("Distribution mains").FirstOrDefault() as BasicFeatureLayer;

        //Search by attribute
        var filter = new ArcGIS.Core.Data.QueryFilter();
        filter.WhereClause = "CONTRACTOR = 'KCGM'";

        var oids = new List<long>();
        using (var rc = disLayer.Search(filter))
        {
          //Create list of oids to update
          while (rc.MoveNext())
          {
            oids.Add(rc.Current.GetObjectID());
          }
        }

        //Create edit operation 
        var modifyOp = new ArcGIS.Desktop.Editing.EditOperation();
        modifyOp.Name = "Update date";

        // load features into inspector and update field
        var dateInsp = new ArcGIS.Desktop.Editing.Attributes.Inspector();
        dateInsp.Load(disLayer, oids);
        dateInsp["InspDate"] = "9/21/2013";

        // modify and execute
        modifyOp.Modify(insp);
        modifyOp.Execute();
      });
      #endregion

      #region Move features

      //Get all of the selected ObjectIDs from the layer.
      var firstLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault();
      var selectionfromMap = firstLayer.GetSelection();

      // set up a dictionary to store the layer and the object IDs of the selected features
      var selectionDictionary = new Dictionary<MapMember, List<long>>();
      selectionDictionary.Add(firstLayer as MapMember, selectionfromMap.GetObjectIDs().ToList());

      var moveFeature = new EditOperation();
      moveFeature.Name = "Move features";
      moveFeature.Move(selectionDictionary, 10, 10);  //specify your units along axis to move the geometry

      moveFeature.Execute();
      #endregion

      #region Move feature to a specific coordinate

      //Get all of the selected ObjectIDs from the layer.
      var abLayer = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault();
      var mySelection = abLayer.GetSelection();
      var selOid = mySelection.GetObjectIDs().FirstOrDefault();

      var moveToPoint = new MapPointBuilder(1.0, 2.0, 3.0, 4.0, MapView.Active.Map.SpatialReference); //can pass in coordinates.

      var modifyFeatureCoord = new EditOperation();
      modifyFeatureCoord.Name = "Move features";
      modifyFeatureCoord.Modify(abLayer, selOid, moveToPoint.ToGeometry());  //Modify the feature to the new geometry 
      modifyFeatureCoord.Execute();

      #endregion

      #region Edit Operation Planarize Features

      var planarizeFeatures = new EditOperation();
      planarizeFeatures.Name = "Planarize Features";

      //Planarize one or more features
      planarizeFeatures.Planarize(featureLayer, new List<long>() { oid });

      //Execute to execute the operation
      //Must be called within QueuedTask.Run
      planarizeFeatures.Execute();

      //or use async flavor
      //await planarizeFeatures.ExecuteAsync();

      #endregion

      #region Edit Operation Reshape Features

      var reshapeFeatures = new EditOperation();
      reshapeFeatures.Name = "Reshape Features";

      reshapeFeatures.Reshape(featureLayer, oid, modifyLine);

      //Reshape a set of features that intersect some geometry....
      var selFeatures = MapView.Active.GetFeatures(modifyLine).Select(
          k => new KeyValuePair<MapMember, List<long>>(k.Key as MapMember, k.Value));

      reshapeFeatures.Reshape(selFeatures, modifyLine);

      //Execute to execute the operation
      //Must be called within QueuedTask.Run
      reshapeFeatures.Execute();

      //or use async flavor
      //await reshapeFeatures.ExecuteAsync();

      #endregion

      var origin = MapPointBuilder.CreateMapPoint(0, 0, null);

      #region Edit Operation Rotate Features

      var rotateFeatures = new EditOperation();
      rotateFeatures.Name = "Rotate Features";

      //Rotate works on a selected set of features
      //Get all features that intersect a polygon
      var rotateSelection = MapView.Active.GetFeatures(polygon).Select(
          k => new KeyValuePair<MapMember, List<long>>(k.Key as MapMember, k.Value));
            
      //Rotate selected features 90 deg about "origin"
      rotateFeatures.Rotate(rotateSelection, origin, Math.PI / 2);

      //Execute to execute the operation
      //Must be called within QueuedTask.Run
      rotateFeatures.Execute();

      //or use async flavor
      //await rotateFeatures.ExecuteAsync();

      #endregion

      #region Edit Operation Scale Features

      var scaleFeatures = new EditOperation();
      scaleFeatures.Name = "Scale Features";

      //Rotate works on a selected set of features
      var scaleSelection = MapView.Active.GetFeatures(polygon).Select(
          k => new KeyValuePair<MapMember, List<long>>(k.Key as MapMember, k.Value));

      //Scale the selected features by 2.0 in the X and Y direction
      scaleFeatures.Scale(scaleSelection, origin, 2.0, 2.0, 0.0);

      //Execute to execute the operation
      //Must be called within QueuedTask.Run
      scaleFeatures.Execute();

      //or use async flavor
      //await scaleFeatures.ExecuteAsync();

      #endregion

      var mp1 = MapPointBuilder.CreateMapPoint(0, 0, null);
      var mp2 = mp1;
      var mp3 = mp1;

      #region Edit Operation Split Features

      var splitFeatures = new EditOperation();
      splitFeatures.Name = "Split Features";

      var splitPoints = new List<MapPoint>() {mp1, mp2, mp3};

      //Split the feature at 3 points
      splitFeatures.Split(featureLayer, oid, splitPoints);

      // split using percentage
      var splitByPercentage = new SplitByPercentage() { Percentage = 33, SplitFromStartPoint = true };
      splitFeatures.Split(featureLayer, oid, splitByPercentage);

      // split using equal parts
      var splitByEqualParts = new SplitByEqualParts() { NumParts = 3 };
      splitFeatures.Split(featureLayer, oid, splitByEqualParts);

      // split using single distance
      var splitByDistance = new SplitByDistance() { Distance = 27.3, SplitFromStartPoint = false };
      splitFeatures.Split(featureLayer, oid, splitByDistance);

      // split using varying distance
      var distances = new List<double>() { 12.5, 38.2, 89.99 };
      var splitByVaryingDistance = new SplitByVaryingDistance() { Distances = distances, SplitFromStartPoint = true, ProportionRemainder = true };
      splitFeatures.Split(featureLayer, oid, splitByVaryingDistance);

      //Execute to execute the operation
      //Must be called within QueuedTask.Run
      splitFeatures.Execute();

      //or use async flavor
      //await splitAtPointsFeatures.ExecuteAsync();

      #endregion

      var linkLayer = featureLayer;

      #region Edit Operation Transform Features

      var transformFeatures = new EditOperation();
      transformFeatures.Name = "Transform Features";

      //Transform a selected set of features
      var transformSelection = MapView.Active.GetFeatures(polygon).Select(
          k => new KeyValuePair<MapMember, List<long>>(k.Key as MapMember, k.Value));

      transformFeatures.Transform(transformSelection, linkLayer);

      //Transform just a layer
      transformFeatures.Transform(featureLayer, linkLayer);

      //Perform an affine transformation
      transformFeatures.TransformAffine(featureLayer, linkLayer);

      //Execute to execute the operation
      //Must be called within QueuedTask.Run
      transformFeatures.Execute();

      //or use async flavor
      //await transformFeatures.ExecuteAsync();

      #endregion

      #region Edit Operation Perform a Clip, Cut, and Planarize

      //Multiple operations can be performed by a single
      //edit operation.
      var clipCutPlanarizeFeatures = new EditOperation();
      clipCutPlanarizeFeatures.Name = "Clip, Cut, and Planarize Features";
      clipCutPlanarizeFeatures.Clip(featureLayer, oid, clipPoly);
      clipCutPlanarizeFeatures.Split(featureLayer, oid, cutLine);
      clipCutPlanarizeFeatures.Planarize(featureLayer, new List<long>() { oid});

      //Note: An edit operation is a single transaction. 
      //Execute the operations (in the order they were declared)
      clipCutPlanarizeFeatures.Execute();

      //or use async flavor
      //await clipCutPlanarizeFeatures.ExecuteAsync();

      #endregion

      #region Edit Operation Chain Edit Operations

      //Chaining operations is a special case. Use "Chained Operations" when you require multiple transactions 
      //to be undo-able with a single "Undo".

      //The most common use case for operation chaining is creating a feature with an attachement. 
      //Adding an attachment requires the object id (of a new feature) has already been created. 
      var editOperation1 = new EditOperation();
      editOperation1.Name = string.Format("Create point in '{0}'", CurrentTemplate.Layer.Name);

      long newFeatureID = -1;
      //The Create operation has to execute so we can get an object_id
      editOperation1.Create(this.CurrentTemplate, polygon, (object_id) => newFeatureID = object_id);
	    //Must be within a QueuedTask
      editOperation1.Execute();
			
	    //or use async flavor
      //await editOperation1.ExecuteAsync();

      //Now, because we have the object id, we can add the attachment.  As we are chaining it, adding the attachment 
	    //can be undone as part of the "Undo Create" operation. In other words, only one undo operation will show on the 
	    //Pro UI and not two.
      var editOperation2 = editOperation1.CreateChainedOperation();
      //Add the attachement using the new feature id
      editOperation2.AddAttachment(this.CurrentTemplate.Layer, newFeatureID, @"C:\data\images\Hydrant.jpg");

      //editOperation1 and editOperation2 show up as a single Undo operation on the UI even though
      //we had two transactions
	    //Must be within a QueuedTask
      editOperation2.Execute();

      //or use async flavor
      //await editOperation2.ExecuteAsync();

      #endregion

      #region SetOnUndone, SetOnRedone, SetOnComitted

      // SetOnUndone, SetOnRedone and SetOnComittedManage can be used to manage 
      // external actions(such as writing to a log table) that are associated with 
      // each edit operation.

      //get selected feature and update attribute
      var selectedFeatures = MapView.Active.Map.GetSelection();
      var testInspector = new Inspector();
      testInspector.Load(selectedFeatures.Keys.First(), selectedFeatures.Values.First());
      testInspector["Name"] = "test";

      //create and execute the edit operation
      var updateTestField = new EditOperation();
      updateTestField.Name = "Update test field";
      updateTestField.Modify(insp);

      //actions for SetOn...
      updateTestField.SetOnUndone(() =>
      {
          //Sets an action that will be called when this operation is undone.
          Debug.WriteLine("Operation is undone");
      });

      updateTestField.SetOnRedone(() =>
      {
          //Sets an action that will be called when this editoperation is redone.
          Debug.WriteLine("Operation is redone");
      });

      updateTestField.SetOnComitted((bool b) => //called on edit session save(true)/discard(false).
      {
          // Sets an action that will be called when this editoperation is committed.
          Debug.WriteLine("Operation is committed");
      });

      updateTestField.Execute();

      #endregion
    }

    #region ProSnippet Group: Row Events
    #endregion

    private static Guid _lastEdit = Guid.Empty;
    #region Stop a delete
    public static void StopADelete()
    {
      // subscribe to the RowDeletedEvent for the appropriate table
      Table table = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault().GetTable();
      RowDeletedEvent.Subscribe(OnRowDeletedEvent, table);
    }

    private static void OnRowDeletedEvent(RowChangedEventArgs obj)
    {
      if (_lastEdit != obj.Guid)
      {
        //cancel with dialog
        // Note - feature edits on Hosted and Standard Feature Services cannot be cancelled.
        obj.CancelEdit("Delete Event\nAre you sure", true);
        _lastEdit = obj.Guid;
      }
    }
    #endregion

    #region Determine if Geometry Changed while editing
    private static FeatureLayer featureLayer;
    private static void DetermineGeometryChange()
    {
      featureLayer = MapView.Active?.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault();
      if (featureLayer == null)
          return;

      QueuedTask.Run(() => {
          //Listen to the RowChangedEvent that occurs when a Row is changed.
          ArcGIS.Desktop.Editing.Events.RowChangedEvent.Subscribe(OnRowChangedEvent, featureLayer.GetTable());
      });
    }
    private static void OnRowChangedEvent(RowChangedEventArgs obj)
    {
      //Get the layer's definition
      var lyrDefn = featureLayer.GetFeatureClass().GetDefinition();
      //Get the shape field of the feature class
      string shapeField = lyrDefn.GetShapeField();
      //Index of the shape field
      var shapeIndex = lyrDefn.FindField(shapeField);
      //Original geometry of the modified row
      var geomOrig = obj.Row.GetOriginalValue(shapeIndex) as Geometry;
      //New geometry of the modified row
      var geomNew = obj.Row[shapeIndex] as Geometry;
      //Compare the two
      bool shapeChanged = geomOrig.IsEqual(geomNew);
    }
    #endregion

    #region Create a record in a separate table within Row Events

    // Use the EditOperation in the RowChangedEventArgs to append actions to be executed. 
    //  Your actions will become part of the operation and combined into one item on the undo stack

    private void HookEvents()
    {
      // subscribe to the RowCreatedEvent
      Table table = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault().GetTable();
      RowCreatedEvent.Subscribe(MyRowCreatedEvent, table);
    }

    private void MyRowCreatedEvent(RowChangedEventArgs obj)
    {
      // get the edit operation
      var parentEditOp = obj.Operation;

      // set up some attributes
      var attribues = new Dictionary<string, object> { };
      attribues.Add("Layer", "Parcels");
      attribues.Add("Description", "OID: " + obj.Row.GetObjectID().ToString() + " " + DateTime.Now.ToShortTimeString());

      //create a record in an audit table
      var sTable = MapView.Active.Map.FindStandaloneTables("EditHistory").First();
      var table = sTable.GetTable();
      parentEditOp.Create(table, attribues);
    }
    #endregion

    #region Modify a record within Row Events
    private void HookChangedEvent()
    {
      // subscribe to the RowChangedEvent
      Table table = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault().GetTable();
      RowChangedEvent.Subscribe(MyRowChangedEvent, table);
    }

    private void MyRowChangedEvent(RowChangedEventArgs obj)
    {
      //example of modifying a field on a row that has been created
      var parentEditOp = obj.Operation;

      // avoid recursion
      if (_lastEdit != obj.Guid)
      {
        //update field on change
        parentEditOp.Modify(obj.Row, "ZONING", "New");

        _lastEdit = obj.Guid;
      }
    }
    #endregion

    #region ProSnippet Group: Inspector
    #endregion

    public async void LoadFirstFeature2Inspector()
    {
      int oid = 0;

      #region Load a feature from a layer into the inspector

      // get the first feature layer in the map
      var firstFeatureLayer = ArcGIS.Desktop.Mapping.MapView.Active.Map.GetLayersAsFlattenedList().
          OfType<ArcGIS.Desktop.Mapping.FeatureLayer>().FirstOrDefault();

      // create an instance of the inspector class
      var inspector = new ArcGIS.Desktop.Editing.Attributes.Inspector();
      // load the feature with ObjectID 'oid' into the inspector
      await inspector.LoadAsync(firstFeatureLayer, oid);

      #endregion
    }

    public async void LoadSelection2Inspector()
    {
      #region Load map selection into Inspector

      // get the currently selected features in the map
      var selectedFeatures = ArcGIS.Desktop.Mapping.MapView.Active.Map.GetSelection();
      // get the first layer and its corresponding selected feature OIDs
      var firstSelectionSet = selectedFeatures.First();

      // create an instance of the inspector class
      var inspector = new ArcGIS.Desktop.Editing.Attributes.Inspector();
      // load the selected features into the inspector using a list of object IDs
      await inspector.LoadAsync(firstSelectionSet.Key, firstSelectionSet.Value);
      #endregion
    }

    public static void InspectorGetAttributeValue()
    {
      QueuedTask.Run(() =>
      {
        #region Get selected feature's attribute value

        // get the currently selected features in the map
        var selectedFeatures = ArcGIS.Desktop.Mapping.MapView.Active.Map.GetSelection();

        // get the first layer and its corresponding selected feature OIDs
        var firstSelectionSet = selectedFeatures.First();

        // create an instance of the inspector class
        var inspector = new ArcGIS.Desktop.Editing.Attributes.Inspector();

        // load the selected features into the inspector using a list of object IDs
        inspector.Load(firstSelectionSet.Key, firstSelectionSet.Value);

        //get the value of
        var pscode = inspector["STATE_NAME"];
        #endregion
      });
    }

    public async void InspectorChangeAttributes()
    {
      #region Load map selection into Inspector and Change Attributes

      // get the currently selected features in the map
      var selectedFeatures = ArcGIS.Desktop.Mapping.MapView.Active.Map.GetSelection();
      // get the first layer and its corresponding selected feature OIDs
      var firstSelectionSet = selectedFeatures.First();

      // create an instance of the inspector class
      var inspector = new ArcGIS.Desktop.Editing.Attributes.Inspector();
      // load the selected features into the inspector using a list of object IDs
      await inspector.LoadAsync(firstSelectionSet.Key, firstSelectionSet.Value);

      // assign the new attribute value to the field "Description"
      // if more than one features are loaded, the change applies to all features
      inspector["Description"] = "The new value.";
      // apply the changes as an edit operation - but with no undo/redo
      await inspector.ApplyAsync();
      #endregion
    }

    #region ProSnippet Group: Accessing Blob Fields
    #endregion

    public static void ReadWriteBlobInspector()
    {
      #region Read and Write blob fields with the attribute inspector
      QueuedTask.Run(() =>
      {
        //get selected feature into inspector
        var selectedFeatures = MapView.Active.Map.GetSelection();
        var insp = new Inspector();
        insp.Load(selectedFeatures.Keys.First(), selectedFeatures.Values.First());

        //read file into memory stream
        var msr = new MemoryStream();
        using (FileStream file = new FileStream(@"d:\images\Hydrant.jpg", FileMode.Open, FileAccess.Read))
        {
          file.CopyTo(msr);
        }

        //put the memory stream in the blob field
        var op = new EditOperation();
        op.Name = "Blob Inspector";
        insp["Blobfield"] = msr;
        op.Modify(insp);
        op.Execute();

        //read a blob field and save to a file
        //assume inspector has been loaded with a feature
        var msw = new MemoryStream();
        msw = insp["Blobfield"] as MemoryStream;
        using (FileStream file = new FileStream(@"d:\temp\blob.jpg", FileMode.Create, FileAccess.Write))
        {
          msw.WriteTo(file);
        }
      });
      #endregion
    }
    
    public static void ReadWriteBlobRow()
    {
      #region Read and Write blob fields with a row cursor in a callback
      QueuedTask.Run(() =>
      {
        var editOp = new EditOperation();
        editOp.Name = "Blob Cursor";
        var featLayer = MapView.Active.Map.FindLayers("Hydrant").First() as FeatureLayer;

        editOp.Callback((context) => {
          using (var rc = featLayer.GetTable().Search(null, false))
          {
            while (rc.MoveNext())
            {
              //read file into memory stream
              var msr = new MemoryStream();
              using (FileStream file = new FileStream(@"d:\images\Hydrant.jpg", FileMode.Open, FileAccess.Read))
              {file.CopyTo(msr);}

              rc.Current["BlobField"] = msr;
              rc.Current.Store();

              //read the blob field to a file
              var msw = new MemoryStream();
              msw = rc.Current["BlobField"] as MemoryStream;
              using (FileStream file = new FileStream(@"d:\temp\blob.jpg", FileMode.Create, FileAccess.Write))
              {msw.WriteTo(file);}
            }
          }
        }, featLayer.GetTable());
        editOp.Execute();
      });
      #endregion
    }

  }

  public class CSVData {
      public Double X, Y, StopOrder, FacilityID;
  }
}
