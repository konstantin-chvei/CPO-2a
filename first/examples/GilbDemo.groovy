def main() {
    def numbers = [25, -1233, 10, 12310, -1512, 7123, -203, 1233, 12321, 12312]
    int sum = 0
    int count = 0

    for (int i = 0; i < numbers.size(); i++) {
        int num = numbers[i]

        if (num > 0) {
            if (num % 2 == 0) {
                sum += num
                count++
            } else if (num % 3 == 0) {
                sum += num * 2
                count++
            } else {
                sum += num.intdiv(2)
            }
        } else if (num < 0) {
            switch (true) {
                case num < -300:
                    sum -= 10
                    break

                case num < -200:
                    sum -= 5
                    break

                case num < -100:
                    if (Math.abs(num) > 75) {
                        sum -= 2
                    }
                    break

                default:
                    sum += num
                    break
            }
        } else {
            count++
        }
    }

    int whileIndex = 0
    while (whileIndex < 2) {
        sum += whileIndex
        whileIndex++
    }

    int doIndex = 0
    do {
        count += doIndex
        doIndex++
    } while (doIndex < 2)

    System.out.printf("Sum: %d, Count: %d%n", sum, count)

    int x = 1
    int y = 0
    boolean isValid = false

    if (x > 0) {
        if (y > 0) {
            isValid = true
        } else if (y == 0) {
            isValid = false
        } else {
            isValid = false
        }
    } else if (x == 0) {
        isValid = y > 0
    } else {
        isValid = false
    }

    println("Valid: ${isValid}")
}

main()