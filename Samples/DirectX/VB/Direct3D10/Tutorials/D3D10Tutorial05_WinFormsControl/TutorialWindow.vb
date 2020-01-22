﻿' Copyright (c) Microsoft Corporation.  All rights reserved.


Imports Microsoft.VisualBasic
Imports System
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports System.Windows.Media.Media3D
Imports Microsoft.WindowsAPICodePack.DirectX.Direct3D
Imports Microsoft.WindowsAPICodePack.DirectX.Direct3D10
Imports Microsoft.WindowsAPICodePack.DirectX.Graphics
Imports Microsoft.WindowsAPICodePack.DirectX.DirectXUtilities

Namespace D3D10Tutorial05_WinFormsControl
	''' <summary>
	''' This application demonstrates animation using matrix transformations
	''' 
	''' http://msdn.microsoft.com/en-us/library/bb172489(VS.85).aspx
	''' 
	''' Copyright (c) Microsoft Corporation. All rights reserved.
	''' </summary>
	Partial Public Class TutorialWindow
		Inherits Form
		#Region "Fields"
		Private device As D3DDevice
		Private swapChain As SwapChain
		Private renderTargetView As RenderTargetView
		Private depthStencil As Texture2D
		Private depthStencilView As DepthStencilView
        Private backColor_Renamed As New ColorRgba(0.0F, 0.125F, 0.3F, 1.0F)

		Private effect As Effect
		Private technique As EffectTechnique
		Private vertexLayout As InputLayout
		Private vertexBuffer As D3DBuffer
		Private indexBuffer As D3DBuffer

		Private worldVariable As EffectMatrixVariable
		Private viewVariable As EffectMatrixVariable
		Private projectionVariable As EffectMatrixVariable

		Private cube As New Cube()

		Private viewMatrix As Matrix4x4F
		Private projectionMatrix As Matrix4x4F

		Private t As Single = 0f
		Private dwTimeStart As UInteger = CUInt(Environment.TickCount)
		Private needsResizing As Boolean
		#End Region

		#Region "TutorialWindow()"
		''' <summary>
		''' Initializes a new instance of the <see cref="TutorialWindow"/> class.
		''' </summary>
		Public Sub New()
			InitializeComponent()
		End Sub
		#End Region

		#Region "TutorialWindow_Load()"
		Private Sub TutorialWindow_Load(ByVal sender As Object, ByVal e As EventArgs) Handles MyBase.Load
			InitDevice()
			directControl.Render = AddressOf Me.RenderScene
		End Sub
		#End Region

		#Region "directControl_SizeChanged()"
		Private Sub directControl_SizeChanged(ByVal sender As Object, ByVal e As EventArgs) Handles directControl.SizeChanged
			needsResizing = True
		End Sub
		#End Region

		#Region "TutorialWindow_FormClosing()"
		Private Sub TutorialWindow_FormClosing(ByVal sender As Object, ByVal e As FormClosingEventArgs) Handles MyBase.FormClosing
			directControl.Render = Nothing
			device.ClearState()
		End Sub
		#End Region

		#Region "InitDevice()"
		''' <summary>
		''' Create Direct3D device and swap chain
		''' </summary>
		Protected Sub InitDevice()
            device = D3DDevice.CreateDeviceAndSwapChain(directControl.Handle)
            swapChain = device.SwapChain

			SetViews()

			' Create the effect
			Using effectStream As FileStream = File.OpenRead("Tutorial05.fxo")
				effect = device.CreateEffectFromCompiledBinary(New BinaryReader(effectStream))
			End Using

			' Obtain the technique
			technique = effect.GetTechniqueByName("Render")

			' Obtain the variables
			worldVariable = effect.GetVariableByName("World").AsMatrix()
			viewVariable = effect.GetVariableByName("View").AsMatrix()
			projectionVariable = effect.GetVariableByName("Projection").AsMatrix()

			InitVertexLayout()
			InitVertexBuffer()
			InitIndexBuffer()

			' Set primitive topology
            device.IA.PrimitiveTopology = PrimitiveTopology.TriangleList

			InitMatrices()
			needsResizing = False
		End Sub
		#End Region

		#Region "SetViews()"
		Private Sub SetViews()
			' Create a render target view
			Using pBuffer As Texture2D = swapChain.GetBuffer(Of Texture2D)(0)
				renderTargetView = device.CreateRenderTargetView(pBuffer)
			End Using

			' Create depth stencil texture
            Dim descDepth As New Texture2DDescription() With {.Width = CUInt(directControl.ClientSize.Width), .Height = CUInt(directControl.ClientSize.Height), .MipLevels = 1, .ArraySize = 1, .Format = Format.D32Float, .SampleDescription = New SampleDescription() With {.Count = 1, .Quality = 0}, .BindingOptions = BindingOptions.DepthStencil}

            depthStencil = device.CreateTexture2D(descDepth)

            ' Create the depth stencil view
            Dim depthStencilViewDesc As New DepthStencilViewDescription() With {.Format = descDepth.Format, .ViewDimension = DepthStencilViewDimension.Texture2D}
            depthStencilView = device.CreateDepthStencilView(depthStencil, depthStencilViewDesc)

            'bind the views to the device
            device.OM.RenderTargets = New OutputMergerRenderTargets(New RenderTargetView() {renderTargetView}, depthStencilView)

            ' Setup the viewport
            Dim vp As New Viewport() With {.Width = CUInt(directControl.ClientSize.Width), .Height = CUInt(directControl.ClientSize.Height), .MinDepth = 0.0F, .MaxDepth = 1.0F, .TopLeftX = 0, .TopLeftY = 0}

            device.RS.Viewports = New Viewport() {vp}
        End Sub
#End Region

#Region "InitVertexLayout()"
        Private Sub InitVertexLayout()
            ' Define the input layout
            ' The layout determines the stride in the vertex buffer,
            ' so changes in layout need to be reflected in SetVertexBuffers
            Dim layout() As InputElementDescription = {New InputElementDescription() With {.SemanticName = "POSITION", .SemanticIndex = 0, .Format = Format.R32G32B32Float, .InputSlot = 0, .AlignedByteOffset = 0, .InputSlotClass = InputClassification.PerVertexData, .InstanceDataStepRate = 0}, New InputElementDescription() With {.SemanticName = "COLOR", .SemanticIndex = 0, .Format = Format.R32G32B32A32Float, .InputSlot = 0, .AlignedByteOffset = 12, .InputSlotClass = InputClassification.PerVertexData, .InstanceDataStepRate = 0}}

            Dim passDesc As PassDescription = technique.GetPassByIndex(0).Description

            vertexLayout = device.CreateInputLayout(layout, passDesc.InputAssemblerInputSignature, passDesc.InputAssemblerInputSignatureSize)

            device.IA.InputLayout = vertexLayout
        End Sub
#End Region

#Region "InitVertexBuffer()"
        Private Sub InitVertexBuffer()
            Dim verticesData As IntPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(cube.Vertices))
            Marshal.StructureToPtr(cube.Vertices, verticesData, True)

            Dim bufferDesc As New BufferDescription() With {.Usage = Usage.Default, .ByteWidth = CUInt(Marshal.SizeOf(cube.Vertices)), .BindingOptions = BindingOptions.VertexBuffer, .CpuAccessOptions = CpuAccessOptions.None, .MiscellaneousResourceOptions = MiscellaneousResourceOptions.None}

            Dim InitData As New SubresourceData() With {.SystemMemory = verticesData}

            'D3DBuffer buffer = null;
            vertexBuffer = device.CreateBuffer(bufferDesc, InitData)

            ' Set vertex buffer
            Dim stride As UInteger = CUInt(Marshal.SizeOf(GetType(SimpleVertex)))
            Dim offset As UInteger = 0
            device.IA.SetVertexBuffers(0, New D3DBuffer() {vertexBuffer}, New UInteger() {stride}, New UInteger() {offset})
            Marshal.FreeCoTaskMem(verticesData)
        End Sub
#End Region

#Region "InitIndexBuffer()"
        Private Sub InitIndexBuffer()
            Dim indicesData As IntPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(cube.Indices))
            Marshal.StructureToPtr(cube.Indices, indicesData, True)

            Dim bufferDesc As New BufferDescription() With {.Usage = Usage.Default, .ByteWidth = CUInt(Marshal.SizeOf(cube.Indices)), .BindingOptions = BindingOptions.IndexBuffer, .CpuAccessOptions = CpuAccessOptions.None, .MiscellaneousResourceOptions = MiscellaneousResourceOptions.None}

            Dim initData As New SubresourceData() With {.SystemMemory = indicesData}

            indexBuffer = device.CreateBuffer(bufferDesc, initData)
            device.IA.IndexBuffer = New IndexBuffer(indexBuffer, Format.R32UInt, 0)
            Marshal.FreeCoTaskMem(indicesData)
        End Sub
#End Region

		#Region "InitMatrices()"
		Private Sub InitMatrices()
			' Initialize the view matrix
			Dim Eye As New Vector3F(0.0f, 4.0f, -10.0f)
			Dim At As New Vector3F(0.0f, 0.0f, 0.0f)
			Dim Up As New Vector3F(0.0f, 1.0f, 0.0f)

            viewMatrix = Microsoft.WindowsAPICodePack.DirectX.DirectXUtilities.Camera.MatrixLookAtLH(Eye, At, Up)

			' Initialize the projection matrix
            projectionMatrix = Microsoft.WindowsAPICodePack.DirectX.DirectXUtilities.Camera.MatrixPerspectiveFovLH(CSng(Math.PI) * 0.25F, (CSng(Me.ClientSize.Width) / CSng(Me.ClientSize.Height)), 0.1F, 100.0F)

			' Update Variables that never change
			viewVariable.Matrix = viewMatrix
			projectionVariable.Matrix = projectionMatrix
		End Sub
		#End Region

		#Region "RenderScene()"
		''' <summary>
		''' Render the frame
		''' </summary>
		Protected Sub RenderScene()
			If needsResizing Then
				needsResizing = False
				renderTargetView.Dispose()
				Dim sd As SwapChainDescription = swapChain.Description
                swapChain.ResizeBuffers(sd.BufferCount, CUInt(directControl.ClientSize.Width), CUInt(directControl.ClientSize.Height), sd.BufferDescription.Format, sd.Options)
				SetViews()
				' Update the projection matrix
                projectionMatrix = Microsoft.WindowsAPICodePack.DirectX.DirectXUtilities.Camera.MatrixPerspectiveFovLH(CSng(Math.PI) * 0.25F, (CSng(directControl.ClientSize.Width) / CSng(directControl.ClientSize.Height)), 0.1F, 100.0F)
				projectionVariable.Matrix = projectionMatrix
			End If
			Dim worldMatrix1 As Matrix4x4F
			Dim worldMatrix2 As Matrix4x4F

			t = (Environment.TickCount - dwTimeStart) / 50.0f

			' 1st Cube: Rotate around the origin
			'WPF transforms used here use degrees as opposed to D3DX which uses radians in the native tutorial
			'360 degrees == 2 * Math.PI
			'world1 matrix rotates the first cube by t degrees
			Dim rt1 As New RotateTransform3D(New AxisAngleRotation3D(New Vector3D(0, 1, 0), t))
			worldMatrix1 = rt1.Value.ToMatrix4x4F()

			' 2nd Cube:  Rotate around the 1st cube
			Dim tg As New Transform3DGroup()
			'spin the cube
			tg.Children.Add(New RotateTransform3D(New AxisAngleRotation3D(New Vector3D(0, 0, 1), -t)))
			'scale it down
			tg.Children.Add(New ScaleTransform3D(0.3, 0.3, 0.3))
			'translate it (move to orbit)
			tg.Children.Add(New TranslateTransform3D(-4, 0, 0))
			'orbit around the big cube
			tg.Children.Add(New RotateTransform3D(New AxisAngleRotation3D(New Vector3D(0, 1, 0), -2 * t)))
			worldMatrix2 = tg.Value.ToMatrix4x4F()

			' Clear the backbuffer
			device.ClearRenderTargetView(renderTargetView, backColor_Renamed)

			' Clear the depth buffer to 1.0 (max depth)
            device.ClearDepthStencilView(depthStencilView, ClearOptions.Depth, 1.0F, CByte(0))

			Dim techDesc As TechniqueDescription = technique.Description

			'
			' Update variables that change once per frame
			'
			worldVariable.Matrix = worldMatrix1

			'
			' Render the 1st cube
			'
            For p As UInteger = 0 To techDesc.Passes - 1UI
                technique.GetPassByIndex(p).Apply()
                device.DrawIndexed(36, 0, 0)
            Next p

			'
			' Render the 2nd cube
			'
			worldVariable.Matrix = worldMatrix2
            For p As UInteger = 0 To techDesc.Passes - 1UI
                technique.GetPassByIndex(p).Apply()
                device.DrawIndexed(36, 0, 0)
            Next p

            swapChain.Present(0, PresentOptions.None)
		End Sub
		#End Region
	End Class
End Namespace
