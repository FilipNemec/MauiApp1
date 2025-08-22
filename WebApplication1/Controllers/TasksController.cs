using Google.Protobuf;
using Microsoft.AspNetCore.Mvc;
using WebApplication1;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("tasks")]
    public class TasksController : ControllerBase
    {
        public TasksController()
        {
            // Constructor logic if needed
        }
    private static readonly TaskList _cachedTaskList = CreateMockTasksStatic();

        [HttpGet("protobuf")]
        public IActionResult GetTasksProtobuf()
        {
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            Response.ContentType = "application/x-protobuf";
            Response.ContentType = "application/x-protobuf";
            _cachedTaskList.WriteTo(Response.Body);
            // The simplest fully asynchronous approach
            //using (var memoryStream = new MemoryStream())
            //{
            //    _cachedTaskList.WriteTo(memoryStream);
            //    memoryStream.Position = 0;
            //    await memoryStream.CopyToAsync(Response.Body);
            //}

            return new EmptyResult();
        }

        [HttpGet("json")]
        public IActionResult GetTasksJson()
        {
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            // Gzip header is now set by middleware
            return Ok(_cachedTaskList);
        }

    private static TaskList CreateMockTasksStatic()
        {
            var taskList = new TaskList();
            for (int i = 1; i <= 100; i++)
            {
                var fieldUuid = Guid.NewGuid().ToString();
                var fieldName = $"Feld {i}";
                var area = 10000 + i * 100;
                var boundary = new Boundary
                {
                    Type = "MultiPolygon",
                    Coordinates = {
                        new MultiPolygon
                        {
                            Polygons = {
                                new Polygon
                                {
                                    Coordinates = {
                                        new Coordinate { Lon = 6.85 + i * 0.01, Lat = 51.10 + i * 0.001 },
                                        new Coordinate { Lon = 6.86 + i * 0.01, Lat = 51.11 + i * 0.001 },
                                        new Coordinate { Lon = 6.87 + i * 0.01, Lat = 51.12 + i * 0.001 }
                                    }
                                }
                            }
                        }
                    }
                };
                var cropSeasons = new Google.Protobuf.Collections.RepeatedField<CropSeason>();
                for (int j = 1; j <= 5; j++)
                {
                    var seasonUuid = Guid.NewGuid().ToString();
                    var sprayings = new Google.Protobuf.Collections.RepeatedField<Spraying>();
                    for (int k = 1; k <= 2; k++)
                    {
                        var sprayingUuid = Guid.NewGuid().ToString();
                        sprayings.Add(new Spraying
                        {
                            Uuid = sprayingUuid,
                            PlannedDate = $"2025-01-{10 + k + j + i}T23:00:00Z",
                            ExecutionDate = $"2025-01-{10 + k + j + i}T23:00:00Z",
                            State = "AUTO_EXECUTED",
                            AssignmentState = "UNASSIGNED",
                            Assignee = "",
                            DosedMap = new DosedMap
                            {
                                Uuid = Guid.NewGuid().ToString(),
                                CreationFlowHint = "CROP_PROTECTION",
                                ApplicationType = "FLAT",
                                ApplicationMode = "SPRAYING",
                                RecipeV2 = {
                                    new Recipe { Name = $"Produkt {k}", Type = "PRODUCT" },
                                    new Recipe { Name = "Wasser", Type = "WATER" }
                                },
                                ActionTypes = { "FUNGICIDE_APPLICATION" }
                            }
                        });
                    }
                    var cropSeason = new CropSeason
                    {
                        Uuid = seasonUuid,
                        IsHealthyField = (j % 2 == 0),
                        LifecycleState = j == 1 ? "ACTIVE" : "PLANNED",
                        Crop = new Crop { Uuid = Guid.NewGuid().ToString() },
                        StartDate = $"2025-04-{10 + j + i}T22:00:00Z",
                        Variety = new Variety { Name = $"Sorte {j}" }
                    };
                    cropSeason.SprayingsV2.AddRange(sprayings);
                    cropSeasons.Add(cropSeason);
                }
                var task = new Task
                {
                    Uuid = fieldUuid,
                    Name = fieldName,
                    Area = area,
                    Boundary = boundary
                };
                task.CropSeasonsV2.AddRange(cropSeasons);
                taskList.Tasks.Add(task);
            }
            return taskList;
        }
    }
}
