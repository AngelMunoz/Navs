namespace Navs.Router

open Navs

type InMemoryHistory =
  interface IHistoryStore

  new: unit -> InMemoryHistory

[<Sealed; Class>]
type Router =

  static member create: config: RouterConfig<'View> -> Result<IRouter<'View>, RouteTableError list>

  [<CompiledName("CreateDefault")>]
  static member createDefault: routes: RouteDefinition<'View> seq -> Result<IRouter<'View>, RouteTableError list>
