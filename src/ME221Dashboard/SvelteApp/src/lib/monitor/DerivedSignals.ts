export interface DerivedSignal {
  id: string;
  name: string;
  unit: string;
  description: string;
  compute: (values: Record<number, number>) => number | null;
}

export const BUILT_IN_DERIVED: DerivedSignal[] = [
  {
    id: 'afr_from_wideband',
    name: 'AFR (Wideband)',
    unit: 'λ',
    description: 'Air-fuel ratio from wideband O2 sensor',
    compute: (v) => {
      const o2 = v[1]; // Wideband O2
      return o2 != null ? o2 : null;
    },
  },
  {
    id: 'boost_from_map',
    name: 'Boost',
    unit: 'psi',
    description: 'Boost pressure from MAP sensor (relative to atmospheric)',
    compute: (v) => {
      const map = v[2]; // MAP sensor
      const atm = v[3]; // Atmospheric pressure
      if (map == null || atm == null || atm === 0) return null;
      return (map - atm) * 0.145038; // kPa to psi
    },
  },
  {
    id: 'fuel_pressure_delta',
    name: 'Fuel Press Delta',
    unit: 'psi',
    description: 'Difference between fuel rail and manifold pressure',
    compute: (v) => {
      const fuel = v[4]; // Fuel rail pressure
      const map = v[2]; // MAP
      if (fuel == null || map == null) return null;
      return (fuel - map) * 0.145038;
    },
  },
  {
    id: 'ignition_correction_total',
    name: 'Ign Correction Sum',
    unit: '°',
    description: 'Sum of all cylinder ignition corrections',
    compute: (v) => {
      const c1 = v[10] ?? 0;
      const c2 = v[11] ?? 0;
      const c3 = v[12] ?? 0;
      const c4 = v[13] ?? 0;
      return c1 + c2 + c3 + c4;
    },
  },
];

export function computeDerivedValue(
  signal: DerivedSignal,
  values: Record<number, number>,
): number | null {
  return signal.compute(values);
}
