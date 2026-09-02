using HattrickAI.V5.Core;

var path = args.Length > 0 ? args[0] : "HattrickAI_V5_CHPP_FullOffline_2026-09-01T08-49-54-690Z(5).json";
return await FullPipelineRegressionRunner.RunAsync(path);
