using HattrickAI.FormationTests;

var failures = 0;
failures += HistoricalFormationMapperTests.RunAll();
failures += SimulationCalibrationTests.RunAll();
failures += VersionSourceTests.RunAll();
return failures;
