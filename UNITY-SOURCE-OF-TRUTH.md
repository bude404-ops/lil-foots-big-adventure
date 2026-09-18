# UNITY SOURCE OF TRUTH (Bude's law, Sept 18 2026)

Big writes Unity Editor automation scripts -> Unity executes them -> Unity creates the
actual GameObjects/assets -> Unity saves them -> Unity imports/compiles -> Unity
validates -> Unity builds the game. The automation script is merely the mechanism for
controlling Unity. It is NOT the game itself.

## SOURCE OF TRUTH

The Unity project is the ONLY source of truth for the final game. The following do NOT
count as the finished game:

- Canvas mockups
- HTML prototypes
- React prototypes
- SVG representations
- screenshots
- Big-generated visual simulations
- external scene representations

They may be used for reference if necessary, but they are never the final implementation.

## DO NOT TRANSLATE A MOCKUP

If an existing Canvas/mockup already exists: do NOT assume its implementation is
correct. Inspect what the design is trying to accomplish and determine how Unity should
implement it natively. Rebuild the feature directly using Unity.

## VALIDATION

After creating a feature:

1. Open/import it through Unity.
2. Compile it.
3. Validate assets.
4. Validate references.
5. Validate scenes/prefabs.
6. Run tests where possible.
7. Build the actual Unity game.

Only report a feature as complete when Unity successfully accepts and builds it.

## THE GOLDEN RULE

NEVER ask: "What can Big create that looks like a Unity game?"
ALWAYS ask: "What does Unity actually need for this feature to exist as a real Unity
game?" Then create THOSE things inside the Unity project.

BIG OPERATES UNITY. UNITY BUILDS THE GAME. THE UNITY PROJECT IS THE PRODUCT.
