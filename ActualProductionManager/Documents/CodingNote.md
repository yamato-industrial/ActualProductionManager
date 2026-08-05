# コーディングノート

## データーベースファーストでのモデルの生成

```cmd
Scaffold-DbContext "Server=192.168.1.81;Port=5432;Database=actual_production;User Id=actual_production;Password=yamato5863111;" Npgsql.EntityFrameworkCore.PostgreSQL -OutputDir Models\Databases -ContextDir Data -Context ActualProductionContext -Tables "dev.setup_times","dev.production_conditions","dev.lines","dev.items" -NoOnConfiguring -Force
```