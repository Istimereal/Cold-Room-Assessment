using System.Data;
using ColdRoomAssessment.Domain;
using ColdRoomAssessment.Infrastructure;
using ColdRoomAssessment.IntegrationTests.Setup;
using Microsoft.EntityFrameworkCore;

namespace ColdRoomAssessment.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class MeasurementPersistenceTests(PostgresFixture postgres) : PersistenceTest(postgres)
{
    [Fact]
    public async Task Data_import_and_measurement_load_from_equipment_with_kind_stored_as_text()
    {
        var measuredAt = new DateTime(2026, 3, 15, 8, 30, 0, DateTimeKind.Unspecified);
        var first = new DateTime(2026, 3, 15, 8, 0, 0, DateTimeKind.Unspecified);
        var last = new DateTime(2026, 3, 15, 9, 0, 0, DateTimeKind.Unspecified);
        var importedAt = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

        Guid equipmentId;
        Guid importId;

        await using (var db = CreateDbContext())
        {
            var facility = new Facility
            {
                Id = Guid.CreateVersion7(),
                Name = "Cold Room A"
            };
            var equipmentType = new EquipmentType
            {
                Id = Guid.CreateVersion7(),
                Name = "DHT22 Logger"
            };
            var equipment = new Equipment
            {
                Id = Guid.CreateVersion7(),
                Facility = facility,
                EquipmentType = equipmentType,
                Name = "SENSOR01",
                Placement = "Near entrance, upper shelf"
            };
            var import = new DataImport
            {
                Id = Guid.CreateVersion7(),
                Equipment = equipment,
                OriginalFileName = "sensor.csv",
                ImportedAt = importedAt,
                FirstMeasurementAt = first,
                LastMeasurementAt = last,
                ImportedRowCount = 2,
                InvalidRowCount = 1,
                Status = ImportStatus.Completed
            };
            db.Add(new Measurement
            {
                Id = Guid.CreateVersion7(),
                DataImport = import,
                MeasuredAt = measuredAt,
                Value = 4.5,
                Kind = MeasurementKind.Temperature,
                Unit = "C"
            });
            db.Add(new Measurement
            {
                Id = Guid.CreateVersion7(),
                DataImport = import,
                MeasuredAt = measuredAt,
                Value = 4.5,
                Kind = MeasurementKind.Temperature,
                Unit = "C"
            });
            await db.SaveChangesAsync();

            equipmentId = equipment.Id;
            importId = import.Id;
        }

        await using (var db = CreateDbContext())
        {
            var equipment = await db.Equipment
                .Include(item => item.DataImports)
                .ThenInclude(import => import.Measurements)
                .SingleAsync(item => item.Id == equipmentId);

            var import = Assert.Single(equipment.DataImports);
            Assert.Equal(importId, import.Id);
            Assert.Equal(equipmentId, import.EquipmentId);
            Assert.Equal("sensor.csv", import.OriginalFileName);
            Assert.Equal(importedAt, import.ImportedAt);
            Assert.Equal(first, import.FirstMeasurementAt);
            Assert.Equal(DateTimeKind.Unspecified, import.FirstMeasurementAt!.Value.Kind);
            Assert.Equal(last, import.LastMeasurementAt);
            Assert.Equal(DateTimeKind.Unspecified, import.LastMeasurementAt!.Value.Kind);
            Assert.Equal(2, import.ImportedRowCount);
            Assert.Equal(1, import.InvalidRowCount);
            Assert.Equal(ImportStatus.Completed, import.Status);

            Assert.Equal(2, import.Measurements.Count);
            Assert.All(import.Measurements, measurement =>
            {
                Assert.Equal(importId, measurement.DataImportId);
                Assert.Equal(measuredAt, measurement.MeasuredAt);
                Assert.Equal(DateTimeKind.Unspecified, measurement.MeasuredAt.Kind);
                Assert.Equal(4.5, measurement.Value);
                Assert.Equal(MeasurementKind.Temperature, measurement.Kind);
                Assert.Equal("C", measurement.Unit);
            });

            var storedKinds = await QueryStringsAsync(
                db,
                """SELECT "Kind" FROM "Measurements" WHERE "DataImportId" = @id ORDER BY "Id" """,
                importId);
            Assert.Equal(new[] { "Temperature", "Temperature" }, storedKinds);

            var storedStatus = await QueryStringAsync(
                db,
                """SELECT "Status" FROM "DataImports" WHERE "Id" = @id""",
                importId);
            Assert.Equal("Completed", storedStatus);
        }

        await using (var db = CreateDbContext())
        {
            Assert.Equal("timestamp without time zone", await ColumnTypeAsync(db, "Measurements", "MeasuredAt"));
            Assert.Equal("timestamp without time zone", await ColumnTypeAsync(db, "DataImports", "FirstMeasurementAt"));
            Assert.Equal("timestamp without time zone", await ColumnTypeAsync(db, "DataImports", "LastMeasurementAt"));
            Assert.Equal("timestamp with time zone", await ColumnTypeAsync(db, "DataImports", "ImportedAt"));
            Assert.Equal("text", await ColumnTypeAsync(db, "Measurements", "Kind"));
            Assert.Equal("double precision", await ColumnTypeAsync(db, "Measurements", "Value"));
        }
    }

    [Fact]
    public async Task Deleting_a_facility_or_import_does_not_cascade_through_measurements()
    {
        Guid facilityId;
        Guid importId;
        Guid measurementId;

        await using (var db = CreateDbContext())
        {
            var facility = new Facility { Id = Guid.CreateVersion7(), Name = "Cold Room A" };
            var equipmentType = new EquipmentType { Id = Guid.CreateVersion7(), Name = "DHT22 Logger" };
            var equipment = new Equipment
            {
                Id = Guid.CreateVersion7(),
                Facility = facility,
                EquipmentType = equipmentType,
                Name = "SENSOR01",
                Placement = "Near entrance"
            };
            var import = new DataImport
            {
                Id = Guid.CreateVersion7(),
                Equipment = equipment,
                Status = ImportStatus.Pending
            };
            var measurement = new Measurement
            {
                Id = Guid.CreateVersion7(),
                DataImport = import,
                MeasuredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
                Value = 1,
                Kind = MeasurementKind.RelativeHumidity,
                Unit = "%"
            };
            db.Add(measurement);
            await db.SaveChangesAsync();

            facilityId = facility.Id;
            importId = import.Id;
            measurementId = measurement.Id;
        }

        await using (var db = CreateDbContext())
        {
            var facility = await db.Facilities.SingleAsync(item => item.Id == facilityId);
            db.Facilities.Remove(facility);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using (var db = CreateDbContext())
        {
            var import = await db.DataImports.SingleAsync(item => item.Id == importId);
            db.DataImports.Remove(import);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using (var db = CreateDbContext())
        {
            Assert.True(await db.Facilities.AnyAsync(item => item.Id == facilityId));
            Assert.True(await db.DataImports.AnyAsync(item => item.Id == importId));
            Assert.True(await db.Measurements.AnyAsync(item => item.Id == measurementId));
        }
    }

    private static async Task<string> ColumnTypeAsync(ColdRoomDbContext db, string table, string column)
    {
        var connection = await OpenAsync(db);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT data_type
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table AND column_name = @column
            """;
        AddParameter(command, "table", table);
        AddParameter(command, "column", column);
        var result = await command.ExecuteScalarAsync();
        return Assert.IsType<string>(result);
    }

    private static async Task<string> QueryStringAsync(ColdRoomDbContext db, string sql, Guid id)
    {
        var values = await QueryStringsAsync(db, sql, id);
        return Assert.Single(values);
    }

    private static async Task<List<string>> QueryStringsAsync(ColdRoomDbContext db, string sql, Guid id)
    {
        var connection = await OpenAsync(db);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameter(command, "id", id);

        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    private static async Task<System.Data.Common.DbConnection> OpenAsync(ColdRoomDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        return connection;
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
