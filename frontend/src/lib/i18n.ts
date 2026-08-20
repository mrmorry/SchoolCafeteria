// Minimal i18n scaffold: Spanish is the only shipped locale, but every UI string is routed
// through t() and a locale is resolvable (localStorage "sc_locale", default "es") so adding an
// "en" dictionary later requires no component changes — just a new entry here.

const dictionaries = {
  es: {
    'app.name': 'SchoolCafeteria',
    'nav.dashboard': 'Panel',
    'nav.students': 'Estudiantes',
    'nav.guardians': 'Tutores',
    'nav.employees': 'Empleados',
    'nav.pos': 'Punto de venta',
    'nav.products': 'Productos',
    'nav.inventory': 'Inventario',
    'nav.reports': 'Reportes',
    'nav.audit': 'Auditoría',
    'nav.settings': 'Configuración',
    'common.save': 'Guardar',
    'common.cancel': 'Cancelar',
    'common.search': 'Buscar',
    'common.loading': 'Cargando…',
    'common.error': 'Ocurrió un error',
    'common.confirm': 'Confirmar'
  }
} as const;

type Locale = keyof typeof dictionaries;
type Key = keyof (typeof dictionaries)['es'];

export function getLocale(): Locale {
  if (typeof window === 'undefined') return 'es';
  return (window.localStorage.getItem('sc_locale') as Locale) || 'es';
}

export function t(key: Key): string {
  const locale = getLocale();
  return dictionaries[locale]?.[key] ?? dictionaries.es[key] ?? key;
}
