﻿//
//  RenderableWorldModel.cs
//
//  Author:
//       Jarl Gullberg <jarl.gullberg@gmail.com>
//
//  Copyright (c) 2017 Jarl Gullberg
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Everlook.Configuration;
using Everlook.Exceptions.Shader;
using Everlook.Utility;
using Everlook.Viewport.Camera;
using Everlook.Viewport.Rendering.Core;
using Everlook.Viewport.Rendering.Interfaces;
using Everlook.Viewport.Rendering.Shaders;
using log4net;
using OpenTK;
using SlimTK;
using Warcraft.WMO;
using Warcraft.WMO.GroupFile;
using Warcraft.WMO.GroupFile.Chunks;
using Warcraft.WMO.RootFile.Chunks;
using BufferTarget = OpenTK.Graphics.OpenGL.BufferTarget;
using BufferUsageHint = OpenTK.Graphics.OpenGL.BufferUsageHint;
using DrawElementsType = OpenTK.Graphics.OpenGL.DrawElementsType;
using EnableCap = OpenTK.Graphics.OpenGL.EnableCap;
using GL = OpenTK.Graphics.OpenGL.GL;
using PrimitiveType = OpenTK.Graphics.OpenGL.PrimitiveType;
using TextureUnit = OpenTK.Graphics.OpenGL.TextureUnit;
using TextureWrapMode = OpenTK.Graphics.OpenGL.TextureWrapMode;
using VertexAttribPointerType = OpenTK.Graphics.OpenGL.VertexAttribPointerType;

namespace Everlook.Viewport.Rendering
{
	/// <summary>
	/// Represents a renderable World Model Object
	/// </summary>
	public sealed class RenderableWorldModel : IRenderable, ITickingActor, IDefaultCameraPositionProvider, IModelInfoProvider
	{
		/// <summary>
		/// Logger instance for this class.
		/// </summary>
		private static readonly ILog Log = LogManager.GetLogger(typeof(RenderableWorldModel));

		/// <summary>
		/// Gets or sets a value indicating whether this object has been disposed.
		/// </summary>
		private bool IsDisposed { get; set; }

		/// <summary>
		/// Gets a value indicating whether this instance uses static rendering; that is,
		/// a single frame is rendered and then reused. Useful as an optimization for images.
		/// </summary>
		/// <value>true</value>
		/// <c>false</c>
		public bool IsStatic => false;

		/// <summary>
		/// Gets the projection method to use for this renderable object. Typically, this is Orthographic
		/// or Perspective.
		/// </summary>
		public ProjectionType Projection => ProjectionType.Perspective;

		/// <summary>
		/// Gets the default camera position for this renderable.
		/// </summary>
		public Vector3 DefaultCameraPosition
		{
			get
			{
				if (!this.IsInitialized)
				{
					return Vector3.Zero;
				}

				if (this.Model.Groups.Count == 0)
				{
					return Vector3.Zero;
				}

				return
				(
					this.ActorTransform.GetModelMatrix() *
					new Vector4
					(
						this.Model.Groups
						.First()
						.GetBoundingBox()
						.GetCenterCoordinates()
						.AsOpenTKVector(),
						1.0f
					)
				)
				.Xyz;
			}
		}

		/// <summary>
		/// The model contained by this renderable world object.
		/// </summary>
		/// <value>The model.</value>
		private readonly WMO Model;

		/// <summary>
		/// Gets or sets the transform of the actor.
		/// </summary>
		public Transform ActorTransform { get; set; }

		private readonly RenderCache Cache = RenderCache.Instance;
		private readonly WarcraftGameContext GameContext;

		/// <summary>
		/// Dictionary that maps texture paths to OpenGL textures.
		/// </summary>
		private readonly Dictionary<string, Texture2D> TextureLookup = new Dictionary<string, Texture2D>();

		// Actual model data
		private readonly Dictionary<ModelGroup, Buffer<Vector3>> VertexBufferLookup = new Dictionary<ModelGroup, Buffer<Vector3>>();
		private readonly Dictionary<ModelGroup, Buffer<Vector3>> NormalBufferLookup = new Dictionary<ModelGroup, Buffer<Vector3>>();
		private readonly Dictionary<ModelGroup, Buffer<Vector2>> TextureCoordinateBufferLookup = new Dictionary<ModelGroup, Buffer<Vector2>>();
		private readonly Dictionary<ModelGroup, Buffer<ushort>> VertexIndexBufferLookup = new Dictionary<ModelGroup, Buffer<ushort>>();

		// Bounding boxes
		private readonly Dictionary<ModelGroup, RenderableBoundingBox> BoundingBoxLookup = new Dictionary<ModelGroup, RenderableBoundingBox>();

		// Doodad sets
		private readonly Dictionary<string, RenderableGameModel> DoodadCache = new Dictionary<string, RenderableGameModel>();
		private readonly Dictionary<string, List<ActorInstanceSet<RenderableGameModel>>> DoodadSets = new Dictionary<string, List<ActorInstanceSet<RenderableGameModel>>>();

		/// <inheritdoc />
		public int PolygonCount => this.Model.Groups.Sum(g => g.GroupData.VertexIndices.VertexIndices.Count / 3);

		/// <inheritdoc />
		public int VertexCount => this.Model.Groups.Sum(g => g.GroupData.Vertices.Vertices.Count);

		/// <summary>
		/// Gets or sets a value indicating whether or not the current renderable has been initialized.
		/// </summary>
		public bool IsInitialized { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether or not the bounding boxes of the model groups should be rendered.
		/// </summary>
		public bool ShouldRenderBounds { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether or not the wireframe of the object should be rendered.
		/// </summary>
		public bool ShouldRenderWireframe { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the doodads in the current doodad set should be rendered.
		/// </summary>
		public bool ShouldRenderDoodads { get; set; }

		/// <summary>
		/// Gets or sets the current doodad set.
		/// </summary>
		public string DoodadSet { get; set; }

		private WorldModelShader Shader;

		/// <summary>
		/// Initializes a new instance of the <see cref="RenderableWorldModel"/> class.
		/// </summary>
		/// <param name="inModel">The model to render.</param>
		/// <param name="gameContext">The game context.</param>
		public RenderableWorldModel(WMO inModel, WarcraftGameContext gameContext)
		{
			this.Model = inModel;
			this.GameContext = gameContext;

			this.ActorTransform = new Transform();

			this.IsInitialized = false;
		}

		/// <summary>
		/// Initializes the required data for rendering.
		/// </summary>
		public void Initialize()
		{
			ThrowIfDisposed();

			if (this.IsInitialized)
			{
				return;
			}

			this.IsInitialized = true;

			this.Shader = this.Cache.GetShader(EverlookShader.WorldModel) as WorldModelShader;

			if (this.Shader == null)
			{
				throw new ShaderNullException(typeof(WorldModelShader));
			}

			InitializeDoodads();

			// TODO: Load and cache sound emitters

			// Load the textures used in this model
			foreach (string texture in this.Model.GetTextures())
			{
				if (!string.IsNullOrEmpty(texture))
				{
					if (!this.TextureLookup.ContainsKey(texture))
					{
						this.TextureLookup.Add(texture, this.Cache.GetTexture(texture, this.GameContext.Assets));
					}
				}
			}

			// TODO: Upload visible block vertices

			// TODO: Upload portal vertices for debug rendering

			// TODO: Load lights into some sort of reasonable structure

			// TODO: Load fog as OpenGL fog

			// TODO: Implement antiportal handling. For now, skip them

			// TODO: Upload convex planes for debug rendering
			foreach (ModelGroup modelGroup in this.Model.Groups)
			{
				InitializeModelGroup(modelGroup);
			}

			this.IsInitialized = true;
		}

		/// <summary>
		/// Initialize the OpenGL state of the world model's referenced doodads.
		/// </summary>
		private void InitializeDoodads()
		{
			foreach (var doodad in this.DoodadCache.Select(d => d.Value))
			{
				doodad.Initialize();
			}

			foreach (var doodadSet in this.DoodadSets)
			{
				foreach (var instanceSet in doodadSet.Value)
				{
					instanceSet.Initialize();
				}
			}
		}

		/// <summary>
		/// Load all of the world model's referenced doodads into memory.
		/// </summary>
		public void LoadDoodads()
		{
			foreach (var doodadSet in this.Model.RootInformation.DoodadSets.DoodadSets)
			{
				var doodadInstances = this.Model.RootInformation.DoodadInstances.DoodadInstances
					.Skip((int)doodadSet.FirstDoodadInstanceIndex)
					.Take((int)doodadSet.DoodadInstanceCount).ToList();

				var doodadInstanceGroups = doodadInstances.GroupBy(d => d.Name);

				var doodadSetInstanceGroups = new List<ActorInstanceSet<RenderableGameModel>>();
				foreach (var doodadInstanceGroup in doodadInstanceGroups)
				{
					var firstInstance = doodadInstanceGroup.First();

					// Check and cache the doodad
					if (string.IsNullOrEmpty(firstInstance.Name))
					{
						Log.Warn("Failed to load doodad. The instance name was null or empty.");
						continue;
					}

					if (!this.DoodadCache.ContainsKey(firstInstance.Name))
					{
						var doodadReference = this.GameContext.GetReferenceForDoodad(firstInstance);
						var doodadModel = DataLoadingRoutines.LoadGameModel(doodadReference);

						if (doodadModel == null)
						{
							Log.Warn($"Failed to load doodad \"{firstInstance.Name}\"");
							continue;
						}

						// Then create a new renderable game model
						var renderableDoodad = new RenderableGameModel(doodadModel, this.GameContext, firstInstance.Name);

						// And cache it
						this.DoodadCache.Add(firstInstance.Name, renderableDoodad);
					}

					var instanceTransforms = new List<Transform>();
					foreach (var doodadInstance in doodadInstanceGroup)
					{
						instanceTransforms.Add
						(
							new Transform
							(
								doodadInstance.Position.AsOpenTKVector(),
								doodadInstance.Orientation.AsOpenTKQuaternion(),
								new Vector3(doodadInstance.Scale)
							)
						);
					}

					var instanceSet = new ActorInstanceSet<RenderableGameModel>(this.DoodadCache[firstInstance.Name]);
					instanceSet.SetInstances(instanceTransforms);

					doodadSetInstanceGroups.Add(instanceSet);
				}

				this.DoodadSets.Add(doodadSet.Name, doodadSetInstanceGroups);
			}
		}

		/// <summary>
		/// Initialize the OpenGL state of the given model group.
		/// </summary>
		/// <param name="modelGroup">The model group to initialize.</param>
		private void InitializeModelGroup(ModelGroup modelGroup)
		{
			/*
				Buffers
			*/

			Buffer<Vector3> vertexBuffer = new Buffer<Vector3>(BufferTarget.ArrayBuffer, BufferUsageHint.StaticDraw);
			Buffer<Vector3> normalBuffer = new Buffer<Vector3>(BufferTarget.ArrayBuffer, BufferUsageHint.StaticDraw);
			Buffer<Vector2> coordinateBuffer = new Buffer<Vector2>(BufferTarget.ArrayBuffer, BufferUsageHint.StaticDraw);
			Buffer<ushort> vertexIndexes = new Buffer<ushort>(BufferTarget.ElementArrayBuffer, BufferUsageHint.StaticDraw);

			// Upload all of the vertices in this group
			vertexBuffer.Data = modelGroup.GetVertices().Select(v => v.AsOpenTKVector()).ToArray();
			this.VertexBufferLookup.Add(modelGroup, vertexBuffer);

			vertexBuffer.AttachAttributePointer(new VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 0, 0));

			// Upload all of the normals in this group
			normalBuffer.Data = modelGroup.GetNormals().Select(v => v.AsOpenTKVector()).ToArray();
			this.NormalBufferLookup.Add(modelGroup, normalBuffer);

			normalBuffer.AttachAttributePointer(new VertexAttributePointer(1, 3, VertexAttribPointerType.Float, 0, 0));

			// Upload all of the UVs in this group
			coordinateBuffer.Data = modelGroup.GetTextureCoordinates().Select(v => v.AsOpenTKVector()).ToArray();
			this.TextureCoordinateBufferLookup.Add(modelGroup, coordinateBuffer);

			coordinateBuffer.AttachAttributePointer(new VertexAttributePointer(2, 2, VertexAttribPointerType.Float, 0, 0));

			// Upload vertex indices for this group
			vertexIndexes.Data = modelGroup.GetVertexIndices().ToArray();
			this.VertexIndexBufferLookup.Add(modelGroup, vertexIndexes);

			RenderableBoundingBox boundingBox = new RenderableBoundingBox
			(
				modelGroup.GetBoundingBox().ToOpenGLBoundingBox(),
				this.ActorTransform
			);

			boundingBox.Initialize();

			this.BoundingBoxLookup.Add(modelGroup, boundingBox);
		}

		/// <summary>
		/// Ticks this actor, advancing or performing any time-based actions.
		/// </summary>
		/// <param name="deltaTime">The time delta, in seconds.</param>
		public void Tick(float deltaTime)
		{
			if (!this.ShouldRenderDoodads)
			{
				return;
			}

			foreach (var doodad in this.DoodadCache.Select(k => k.Value))
			{
				doodad.Tick(deltaTime);
			}
		}

		/// <inheritdoc />
		public void Render(Matrix4 viewMatrix, Matrix4 projectionMatrix, ViewportCamera camera)
		{
			ThrowIfDisposed();

			if (!this.IsInitialized)
			{
				return;
			}

			this.Shader.Enable();
			this.Shader.Wireframe.Enabled = this.ShouldRenderWireframe;
			if (this.Shader.Wireframe.Enabled)
			{
				this.Shader.Wireframe.SetWireframeLineWidth(2);
				this.Shader.Wireframe.SetWireframeFadeWidth(2);
				this.Shader.Wireframe.SetViewportMatrix(camera.GetViewportMatrix());
			}

			Matrix4 modelView = this.ActorTransform.GetModelMatrix() * viewMatrix;
			Matrix4 modelViewProjection = modelView * projectionMatrix;

			// TODO: Fix frustum culling
			foreach (ModelGroup modelGroup in this.Model.Groups
				.OrderByDescending(modelGroup => VectorMath.Distance(camera.Position, modelGroup.GetPosition().AsOpenTKVector())))
			{
				RenderGroup(modelGroup, modelViewProjection);
			}

			// Render bounding boxes
			if (this.ShouldRenderBounds)
			{
				foreach (ModelGroup modelGroup in this.Model.Groups
					.OrderByDescending(modelGroup => VectorMath.Distance(camera.Position, modelGroup.GetPosition().AsOpenTKVector())))
				{
					this.BoundingBoxLookup[modelGroup].Render(viewMatrix, projectionMatrix, camera);
				}
			}

			if (this.ShouldRenderDoodads)
			{
				foreach (var doodadInstanceSet in this.DoodadSets[this.DoodadSet])
				{
					//doodadInstanceSet.ShouldRenderBounds = this.ShouldRenderBounds;
				}

				foreach (var doodadInstanceSet in this.DoodadSets[this.DoodadSet])
				{
					doodadInstanceSet.Render(viewMatrix, projectionMatrix, camera);
				}
			}

			// TODO: Summarize the render batches from each group that has the same material ID

			// TODO: Render each block of batches with the same material ID

			// TODO: Shade light effects and vertex colours

			// TODO: Render each doodad in the currently selected doodad set

			// TODO: Play sound emitters here?
		}

		/// <summary>
		/// Renders the specified model group on a batch basis.
		/// </summary>
		private void RenderGroup(ModelGroup modelGroup, Matrix4 modelViewProjection)
		{
			// Reenable depth test
			GL.Enable(EnableCap.DepthTest);

			// Render the object
			// Send the vertices to the shader
			this.VertexBufferLookup[modelGroup].Bind();
			this.VertexBufferLookup[modelGroup].EnableAttributes();

			this.NormalBufferLookup[modelGroup].Bind();
			this.NormalBufferLookup[modelGroup].EnableAttributes();

			this.TextureCoordinateBufferLookup[modelGroup].Bind();
			this.TextureCoordinateBufferLookup[modelGroup].EnableAttributes();

			// Bind the index buffer
			this.VertexIndexBufferLookup[modelGroup].Bind();

			if (this.ShouldRenderWireframe)
			{
				this.Shader.Wireframe.SetWireframeColour(EverlookConfiguration.Instance.WireframeColour);

				// Override blend setting
				GL.Enable(EnableCap.Blend);
			}

			// Render all the different materials (opaque first, transparent after)
			foreach (RenderBatch renderBatch in modelGroup.GetRenderBatches()
				.OrderBy(batch => batch.MaterialIndex)
				.ThenBy(batch => this.Model.GetMaterial(batch.MaterialIndex).BlendMode))
			{
				this.Shader.Enable();

				ModelMaterial modelMaterial = this.Model.GetMaterial(renderBatch.MaterialIndex);

				this.Shader.SetMaterial(modelMaterial);
				this.Shader.SetMVPMatrix(modelViewProjection);

				// Set the texture as the first diffuse texture in unit 0
				Texture2D texture = this.Cache.GetCachedTexture(modelMaterial.Texture0);
				if (modelMaterial.Flags.HasFlag(MaterialFlags.TextureWrappingClamp))
				{
					texture.WrappingMode = TextureWrapMode.Clamp;
				}
				else
				{
					texture.WrappingMode = TextureWrapMode.Repeat;
				}

				this.Shader.BindTexture2D(TextureUnit.Texture0, TextureUniform.Texture0, texture);

				// Finally, draw the model
				GL.DrawRangeElements
				(
					PrimitiveType.Triangles,
					renderBatch.FirstPolygonIndex,
					renderBatch.FirstPolygonIndex + renderBatch.PolygonIndexCount - 1,
					renderBatch.PolygonIndexCount,
					DrawElementsType.UnsignedShort,
					new IntPtr(renderBatch.FirstPolygonIndex * 2)
				);
			}

			// Release the attribute arrays
			this.VertexBufferLookup[modelGroup].DisableAttributes();
			this.NormalBufferLookup[modelGroup].DisableAttributes();
			this.TextureCoordinateBufferLookup[modelGroup].DisableAttributes();
		}

		/// <summary>
		/// Gets the names of the doodad sets for this model.
		/// </summary>
		/// <returns>The names of the doodad sets.</returns>
		public IEnumerable<string> GetDoodadSetNames()
		{
			return this.Model.RootInformation.DoodadSets.DoodadSets.Select(ds => ds.Name);
		}

		/// <summary>
		/// Throws an <see cref="ObjectDisposedException"/> if the object has been disposed.
		/// </summary>
		/// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
		private void ThrowIfDisposed()
		{
			if (this.IsDisposed)
			{
				throw new ObjectDisposedException(ToString());
			}
		}

		/// <summary>
		/// Releases all resource used by the <see cref="RenderableWorldModel"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="RenderableWorldModel"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="RenderableWorldModel"/> in an unusable state.
		/// After calling <see cref="Dispose"/>, you must release all references to the
		/// <see cref="RenderableWorldModel"/> so the garbage collector can reclaim the memory that the
		/// <see cref="RenderableWorldModel"/> was occupying.</remarks>
		public void Dispose()
		{
			this.IsDisposed = true;

			this.Model.Dispose();

			foreach (var vertexBuffer in this.VertexBufferLookup)
			{
				vertexBuffer.Value.Dispose();
			}

			foreach (var normalBuffer in this.NormalBufferLookup)
			{
				normalBuffer.Value.Dispose();
			}

			foreach (var coordinateBuffer in this.TextureCoordinateBufferLookup)
			{
				coordinateBuffer.Value.Dispose();
			}

			foreach (var indexBuffer in this.VertexIndexBufferLookup)
			{
				indexBuffer.Value.Dispose();
			}

			foreach (var doodad in this.DoodadCache.Select(k => k.Value))
			{
				doodad.Dispose();
			}
		}

		/// <summary>
		/// Determines whether or not this object is equal to another object.
		/// </summary>
		/// <param name="obj">The other object</param>
		/// <returns>true if the objects are equal; false otherwise.</returns>
		public override bool Equals(object obj)
		{
			var otherModel = obj as RenderableWorldModel;
			if (otherModel == null)
			{
				return false;
			}

			return (otherModel.Model == this.Model) &&
					(otherModel.GameContext == this.GameContext) &&
					(otherModel.IsStatic == this.IsStatic);
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="RenderableGameModel"/> object.
		/// </summary>
		/// <returns>A hash code for this instance that is suitable for use in hashing algorithms and data structures such as a hash table.</returns>
		public override int GetHashCode()
		{
			return (this.IsStatic.GetHashCode() + this.Model.GetHashCode() + this.GameContext.GetHashCode()).GetHashCode();
		}
	}
}
