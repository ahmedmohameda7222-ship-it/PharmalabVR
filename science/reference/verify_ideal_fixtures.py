"""Independent ideal reference checks. This is not the product scientific engine."""
from pathlib import Path
import json, math

def charge_residual(ph, case, constants):
    h = 10.0 ** (-ph)
    volume_l = case['volumeM3'] * 1000.0
    sodium = case['sodiumMol'] / volume_l
    chloride = case['chlorideMol'] / volume_l
    acetate = case['acetateMol'] / volume_l
    return h + sodium - chloride - acetate * constants['Ka'] / (constants['Ka'] + h) - constants['Kw'] / h

def solve(case, constants):
    assert case['volumeM3'] > 0
    assert all(math.isfinite(case[k]) and case[k] >= 0 for k in ['volumeM3','sodiumMol','chlorideMol','acetateMol'])
    lo, hi = -2.0, 16.0
    assert charge_residual(lo, case, constants) > 0
    assert charge_residual(hi, case, constants) < 0
    for _ in range(200):
        mid = (lo + hi) / 2
        if charge_residual(mid, case, constants) > 0:
            lo = mid
        else:
            hi = mid
    ph = (lo + hi) / 2
    return ph, charge_residual(ph, case, constants)

def verify(root=None):
    if root is not None:
        fixture_path = Path(root)/'fixtures/ideal-aqueous.json'
    else:
        adjacent = Path(__file__).resolve().parent/'ideal-aqueous.json'
        fixture_path = adjacent if adjacent.is_file() else Path(__file__).resolve().parent.parent/'fixtures/ideal-aqueous.json'
    data = json.loads(fixture_path.read_text(encoding='utf-8'))
    max_error = max_residual = 0.0
    for case in data['cases']:
        ph, residual = solve(case, data['constants'])
        error = abs(ph - case['expectedPH'])
        max_error, max_residual = max(max_error,error), max(max_residual,abs(residual))
        assert error <= 1e-8, (case['id'],error)
        assert abs(residual) <= 1e-12, (case['id'],residual)
    # Independent analytic strong-acid/base anchors under the same assumptions.
    anchors = {'strong-0':1.0000000000004343,'strong-12.5':1.4771212547235715,
               'strong-25':7.0,'strong-37.5':12.301029995674838}
    indexed={c['id']:c for c in data['cases']}
    for name,expected in anchors.items():
        assert abs(indexed[name]['expectedPH']-expected)<1e-8, name
    return {'scope':'independent ideal numeric reference only','cases':len(data['cases']),
            'maxExpectedPHError':max_error,'maxChargeResidualMolPerL':max_residual,
            'productEngineTested':False,'experimentalAccuracyEstablished':False}

if __name__=='__main__':
    print(json.dumps(verify(),indent=2))
