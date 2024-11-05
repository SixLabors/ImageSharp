// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

internal class Av1FrameEncoder
{
    private readonly Av1FrameBuffer frameBuffer;

    public Av1FrameEncoder(Av1FrameBuffer frameBuffer)
    {
        this.frameBuffer = frameBuffer;
    }

    /// <summary>
    /// SVT: svt_av1_enc_init
    /// </summary>
    public static void Encode()
    {
        /************************************
        * Thread Handles
        ************************************/

        // Resource Coordination
        // Single thread calling svt_aom_resource_coordination_kernel with context enc_handle_ptr->resource_coordination_context_ptr
        // Multiple threads calling svt_aom_picture_analysis_kernel, with context enc_handle_ptr->picture_analysis_context_ptr_array

        // Picture Decision
        // Single thread calling svt_aom_picture_decision_kernel with context enc_handle_ptr->picture_decision_context_ptr

        // Motion Estimation
        // Multiple threads calling svt_aom_motion_estimation_kernel with context enc_handle_ptr->motion_estimation_context_ptr_array

        // Initial Rate Control
        // Single thread calling svt_aom_initial_rate_control_kernel with context enc_handle_ptr->initial_rate_control_context_ptr

        // Source Based Oprations
        // <Multiple threads calling svt_aom_source_based_operations_kernel with context enc_handle_ptr->source_based_operations_context_ptr_array

        // TPL dispenser
        // Multiple threads calling svt_aom_tpl_disp_kernel with context enc_handle_ptr->tpl_disp_context_ptr_array

        // Picture Manager
        // Single thread calling svt_aom_picture_manager_kernel with context enc_handle_ptr->picture_manager_context_ptr

        // Rate Control
        // Single thread calling svt_aom_rate_control_kernel with context enc_handle_ptr->rate_control_context_ptr

        // Mode Decision Configuration Process
        // Multiple threads calling svt_aom_mode_decision_configuration_kernel with context enc_handle_ptr->mode_decision_configuration_context_ptr_array

        // EncDec Process
        // Multiple threads calling svt_aom_mode_decision_kernel enc_handle_ptr->enc_dec_context_ptr_array

        // Dlf Process
        // Multiple threads calling svt_aom_dlf_kernel with context enc_handle_ptr->dlf_context_ptr_array

        // Cdef Process
        // Multiple threads calling svt_aom_cdef_kernel enc_handle_ptr->cdef_context_ptr_array

        // Rest Process
        // Multiple threads calling svt_aom_rest_kernel enc_handle_ptr->rest_context_ptr_array

        // Entropy Coding Process
        // Multiple threads calling svt_aom_entropy_coding_kernel enc_handle_ptr->entropy_coding_context_ptr_array

        // Packetization
        // Single thread calling svt_aom_packetization_kernel with context enc_handle_ptr->packetization_context_ptr

        // svt_print_memory_usage();
    }
}
