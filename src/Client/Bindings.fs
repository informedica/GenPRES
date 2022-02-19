namespace Feliz.Markdown

open Feliz
open Fable.Core
open Fable.Core.JsInterop

type ITextProperties =
    abstract children: string

type IParagraphProperties =
    abstract children: ReactElement []

type ILinkProperties =
    abstract href: string
    abstract children: ReactElement []

type IHeadingProperties =
    abstract level: int
    abstract children: ReactElement []

type ITableProperties =
    abstract children: ReactElement []

type ITableHeadProperties =
    abstract children: ReactElement []

type ITableBodyProperties =
    abstract children: ReactElement []

type ITableRowProperties =
    abstract children: ReactElement []

type ITableCellProperties =
    abstract isHeader: bool
    abstract children: ReactElement []

type IListProperties =
    abstract children: ReactElement []

type IListItemProperties =
    abstract children: ReactElement []

type IPluginsProperties =
    abstract children: ReactElement []

type IComponent =
    interface
    end

module markdown =

    type components =

        static member inline p
            (component': IParagraphProperties -> ReactElement)
            =
            unbox<IComponent> (Interop.mkAttr "p" component')

        static member inline linkReference
            (component': ILinkProperties -> ReactElement)
            =
            unbox<IComponent> (Interop.mkAttr "linkReference" component')

        static member inline h1
            (component': IHeadingProperties -> ReactElement)
            =
            unbox<IComponent> (Interop.mkAttr "h1" component')

        static member inline h2
            (component': IHeadingProperties -> ReactElement)
            =
            unbox<IComponent> (Interop.mkAttr "h2" component')

        static member inline h3
            (component': IHeadingProperties -> ReactElement)
            =
            unbox<IComponent> (Interop.mkAttr "h3" component')

        static member inline h4
            (component': IHeadingProperties -> ReactElement)
            =
            unbox<IComponent> (Interop.mkAttr "h4" component')

        static member inline h5
            (component': IHeadingProperties -> ReactElement)
            =
            unbox<IComponent> (Interop.mkAttr "h5" component')

        static member inline h6
            (component': IHeadingProperties -> ReactElement)
            =
            unbox<IComponent> (Interop.mkAttr "h6" component')

        static member inline table
            (component': ITableProperties -> ReactElement)
            =
            unbox<IComponent> (Interop.mkAttr "table" component')

        static member inline thead
            (component': ITableHeadProperties -> ReactElement)
            =
            unbox<IComponent> (Interop.mkAttr "thead" component')

        static member inline tbody
            (component': ITableBodyProperties -> ReactElement)
            =
            unbox<IComponent> (Interop.mkAttr "tbody" component')

        static member inline trow
            (component': ITableRowProperties -> ReactElement)
            =
            unbox<IComponent> (Interop.mkAttr "trow" component')

        static member inline tcell
            (component': ITableCellProperties -> ReactElement)
            =
            unbox<IComponent> (Interop.mkAttr "tcell" component')

        static member inline ol(component': IListProperties -> ReactElement) =
            unbox<IComponent> (Interop.mkAttr "ol" component')

        static member inline ul(component': IListProperties -> ReactElement) =
            unbox<IComponent> (Interop.mkAttr "ul" component')

        static member inline li(component': IListProperties -> ReactElement) =
            unbox<IComponent> (Interop.mkAttr "li" component')


[<Erase>]
type markdown =

    /// The Markdown source to parse (**REQUIRED**).
    static member inline children(value: string) =
        Interop.mkAttr "children" value

    static member inline components(components: IComponent list) =
        Interop.mkAttr "components" (createObj !!components)