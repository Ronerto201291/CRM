# ADR-XXXX: <Nombre>

> Este archivo es la plantilla base para las Architecture Decision Records (ADR) de
> este proyecto. A diferencia de las ADR clásicas (que documentan una decisión
> futura antes de implementarla), las ADR de esta carpeta documentan la
> **estructura actual y ya construida** ("as-built") de cada módulo del ERP, tal
> y como existe hoy en la rama `main`. Su objetivo es dar a cualquier sesión de
> trabajo futura (humana o de IA) contexto fiable y verificado contra el código,
> para poder modificar un módulo sin tener que releer todo el repositorio desde
> cero. Se numeran secuencialmente (0000, 0001, 0002...) y se guardan en
> `/home/user/CRM/ErpProject/docs/adr/`. Copia este archivo para crear una ADR
> nueva y sustituye los marcadores `<...>` por contenido real, verificado leyendo
> el código fuente (no se debe inventar ningún endpoint, entidad o regla de
> negocio que no exista en el repositorio). Incluye una sección obligatoria
> de **Evaluación de calidad arquitectónica** (SOLID, Clean Architecture,
> estructura de carpetas, duplicación, controllers delgados, escalabilidad,
> CQRS) — ver la metodología completa y los hallazgos transversales en
> `ADR-0018`.

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
<Por qué existe este módulo/decisión, qué problema de negocio o técnico
resuelve, y qué alcance cubre esta ADR. Mencionar en qué carpetas del
repositorio vive el código descrito (backend/Modules/<Modulo>, frontend/src/app/<ruta>, etc.).>

## Decisión
### Backend
<Estructura de carpetas (Api/Application/Domain/Infrastructure), entidades de
dominio principales, comandos/queries CQRS relevantes (MediatR), DbContext
propio y su esquema de PostgreSQL, controllers y endpoints principales,
servicios de infraestructura relevantes. Citar rutas de archivo reales.>

### Frontend
<Rutas de Next.js App Router involucradas, componentes/páginas principales,
llamadas a la API, manejo de estado relevante.>

### Modelo de datos
<Entidades/tablas principales y sus relaciones clave. No es necesario listar
cada columna; centrarse en las relaciones y campos que importan para
entender el módulo.>

### Flujo end-to-end representativo
<Un flujo de usuario real, trazado desde el frontend hasta la base de datos
(y de vuelta), citando los archivos/métodos concretos que participan.>

## Relación con otros módulos
<Qué módulos consumen o son consumidos por este, vía eventos de dominio
(Outbox), llamadas directas, o dependencia de infraestructura compartida
(multi-tenancy, permisos, etc.).>

## Evaluación de calidad arquitectónica
> Sección obligatoria en todo ADR nuevo o revisado. Ver metodología completa
> y hallazgos transversales en `ADR-0018` — aquí solo se responde el
> checklist para ESTE módulo/pieza concreto, citando archivo real (no
> inventar ni asumir cumplimiento sin verificar el código).

- **SOLID**: ¿alguna clase concentra más de una razón de cambio (SRP)? ¿hay
  reglas de negocio que cambian con frecuencia codificadas como
  switch/diccionario en vez de datos (OCP)? ¿alguna clase derivada rompe el
  contrato de su base añadiendo precondiciones/excepciones no anunciadas
  (LSP)? ¿alguna interfaz obliga a depender de miembros que el consumidor no
  usa (ISP)? ¿el Application layer depende de abstracciones o de tipos
  concretos de Infrastructure (DIP)?
- **Clean Architecture**: ¿las referencias de proyecto (`.csproj`) solo
  fluyen hacia adentro (Api→Application→Domain, Infrastructure→Application/Domain)?
  ¿el Domain está libre de EF Core/ASP.NET? ¿este módulo depende de otros
  módulos (o el core depende de él) sin pasar por una interfaz/evento?
- **Estructura de carpetas**: ¿tiene 4 `.csproj` separados
  (Api/Application/Domain/Infrastructure) con frontera real de compilador
  entre capas, o es un único assembly donde esa frontera es solo organizativa?
- **Duplicación**: ¿existe la misma lógica de negocio implementada dos veces
  (controller vs. handler, o entre dos módulos)?
- **Código limpio — controllers delgados**: ¿cada controller solo construye
  un Command/Query y llama a `_mediator.Send(...)`, o tiene lógica de
  negocio, acceso a datos o construcción de entidades inline?
- **Escalabilidad**: ¿los endpoints de listado paginan? ¿hay queries dentro
  de bucles (N+1)? ¿hay configuración/URLs hardcodeadas que deberían venir de
  `IConfiguration`?
- **CQRS**: ¿todo el flujo pasa por `IMediator` con Command/Query separados,
  o algún controller bypasea el pipeline? ¿el módulo registra sus propios
  validators de FluentValidation en su `DependencyInjection.cs`
  (`AddValidatorsFromAssembly`), o existen como clase pero nunca se ejecutan?

## Buenas prácticas aplicables
<Convenciones a seguir al modificar este módulo: patrones ya usados en el
código, validaciones esperadas, cuidado con filtros multi-tenant, etc.>

## Consecuencias
<Compromisos conocidos, deuda técnica, inconsistencias detectadas en el
código (por ejemplo, algo registrado en un sitio pero no en otro), y
riesgos a tener en cuenta al trabajar en este módulo.>
