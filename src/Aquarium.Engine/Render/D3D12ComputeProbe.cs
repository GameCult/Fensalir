using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Vortice.Direct3D;
using Vortice.Direct3D12;

namespace Aquarium.Engine.Render;

internal static class D3D12ComputeProbe
{
    public static unsafe TOutput[] Run<TInput, TOutput>(string shaderPath, string entryPoint, ReadOnlySpan<TInput> inputs)
        where TInput : unmanaged where TOutput : unmanaged
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("D3D12 probes require Windows.");
        var inputBytes = checked(inputs.Length * Unsafe.SizeOf<TInput>());
        var outputBytes = checked(inputs.Length * Unsafe.SizeOf<TOutput>());
        using var device = D3D12.D3D12CreateDevice<ID3D12Device>(IntPtr.Zero, FeatureLevel.Level_11_0);
        using var queue = device.CreateCommandQueue(new CommandQueueDescription(CommandListType.Direct));
        using var allocator = device.CreateCommandAllocator(CommandListType.Direct);
        using var list = device.CreateCommandList<ID3D12GraphicsCommandList>(0, CommandListType.Direct, allocator, null);
        using var fence = device.CreateFence(0);
        using var fenceEvent = new EventWaitHandle(false, EventResetMode.AutoReset);
        using var input = device.CreateCommittedResource(HeapType.Upload, ResourceDescription.Buffer((ulong)inputBytes), ResourceStates.GenericRead);
        using var output = device.CreateCommittedResource(HeapType.Default, ResourceDescription.Buffer((ulong)outputBytes, ResourceFlags.AllowUnorderedAccess), ResourceStates.UnorderedAccess);
        using var readback = device.CreateCommittedResource(HeapType.Readback, ResourceDescription.Buffer((ulong)outputBytes), ResourceStates.CopyDest);

        var mappedInput = input.Map<byte>(0);
        MemoryMarshal.AsBytes(inputs).CopyTo(new Span<byte>(mappedInput, inputBytes));
        input.Unmap(0);

        var parameters = new[]
        {
            new RootParameter(RootParameterType.ShaderResourceView, new RootDescriptor(0, 0), ShaderVisibility.All),
            new RootParameter(RootParameterType.UnorderedAccessView, new RootDescriptor(0, 0), ShaderVisibility.All),
            new RootParameter(new RootConstants(0, 0, 1), ShaderVisibility.All),
        };
        var signatureDescription = new RootSignatureDescription(RootSignatureFlags.None, parameters, []);
        using var signature = device.CreateRootSignature(0, in signatureDescription, RootSignatureVersion.Version1);
        using var pipeline = device.CreateComputePipelineState(new ComputePipelineStateDescription
        {
            RootSignature = signature,
            ComputeShader = D3D12ShaderCompiler.Compile(shaderPath, entryPoint, "cs_5_0"),
        });
        list.SetComputeRootSignature(signature);
        list.SetPipelineState(pipeline);
        list.SetComputeRootShaderResourceView(0, input.GPUVirtualAddress);
        list.SetComputeRootUnorderedAccessView(1, output.GPUVirtualAddress);
        list.SetComputeRoot32BitConstant(2, (uint)inputs.Length, 0);
        list.Dispatch((uint)((inputs.Length + 63) / 64), 1, 1);
        list.ResourceBarrier(ResourceBarrier.BarrierTransition(output, ResourceStates.UnorderedAccess, ResourceStates.CopySource));
        list.CopyBufferRegion(readback, 0, output, 0, (ulong)outputBytes);
        list.Close();
        queue.ExecuteCommandList(list);
        queue.Signal(fence, 1).CheckError();
        fence.SetEventOnCompletion(1, fenceEvent.SafeWaitHandle.DangerousGetHandle()).CheckError();
        fenceEvent.WaitOne();

        var results = new TOutput[inputs.Length];
        var mappedOutput = readback.Map<byte>(0);
        new ReadOnlySpan<byte>(mappedOutput, outputBytes).CopyTo(MemoryMarshal.AsBytes(results.AsSpan()));
        readback.Unmap(0);
        return results;
    }
}
